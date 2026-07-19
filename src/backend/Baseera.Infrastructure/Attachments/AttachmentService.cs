namespace Baseera.Infrastructure.Attachments;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Domain.Attachments;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "baseera-attachments");
}

public sealed class LocalFileStorage(IOptions<AttachmentStorageOptions> options) : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath);

    public async Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var safeName = Path.GetFileName(storedFileName);
        var path = Path.GetFullPath(Path.Combine(_root, safeName));
        if (!path.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("مسار التخزين غير صالح.");
        }

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return new StoredFileResult(path);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(storagePath);
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("مسار الملف خارج مساحة التخزين المسموحة.");
        }

        if (!File.Exists(full))
        {
            throw new FileNotFoundException("الملف غير متوفر.");
        }

        Stream stream = File.OpenRead(full);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(storagePath);
        if (full.StartsWith(_root, StringComparison.OrdinalIgnoreCase) && File.Exists(full))
        {
            File.Delete(full);
        }

        return Task.CompletedTask;
    }
}

public sealed class AttachmentService(
    BaseeraDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IOrganizationalScopeService scope,
    IAuditService audit) : IAttachmentService
{
    public async Task<Attachment> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!AttachmentEntityTypes.IsAllowed(request.EntityType))
        {
            throw new InvalidOperationException("نوع الكيان غير مدعوم للمرفقات.");
        }

        if (request.SizeBytes <= 0 || request.SizeBytes > AttachmentRules.MaxSizeBytes)
        {
            throw new InvalidOperationException($"حجم الملف غير مسموح. الحد الأقصى {AttachmentRules.MaxSizeBytes} بايت.");
        }

        if (!AttachmentRules.AllowedContentTypes.Contains(request.ContentType))
        {
            throw new InvalidOperationException("نوع الملف غير مسموح.");
        }

        await EnsureEntityInScopeAsync(request.EntityType, request.EntityId, cancellationToken);

        AttachmentRules.ValidateMagicBytes(request.Content, request.ContentType, request.OriginalFileName);

        var original = AttachmentRules.SanitizeFileName(request.OriginalFileName);
        var sha = AttachmentRules.ComputeSha256(request.Content);
        var storedName = $"{Guid.NewGuid():N}";
        string? savedPath = null;

        try
        {
            var saved = await storage.SaveAsync(request.Content, storedName, cancellationToken);
            savedPath = saved.StoragePath;

            var entity = new Attachment
            {
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                OriginalFileName = original,
                StoredFileName = storedName,
                ContentType = request.ContentType,
                SizeBytes = request.SizeBytes,
                Sha256 = sha,
                UploadedBy = currentUser.ExternalSubject ?? "unknown",
                Classification = request.Classification,
                UploadReason = request.UploadReason,
                ScanStatus = AttachmentScanStatus.PendingScan,
                StoragePath = saved.StoragePath,
                CreatedBy = currentUser.ExternalSubject
            };

            db.Attachments.Add(entity);
            await audit.WriteAsync(new AuditEntry
            {
                Action = "Upload",
                Module = "Attachments",
                EntityType = nameof(Attachment),
                EntityId = entity.Id.ToString(),
                NewValues = new { entity.OriginalFileName, entity.ContentType, entity.SizeBytes, entity.Sha256, entity.Classification, entity.ScanStatus },
                Reason = request.UploadReason
            }, cancellationToken);

            await db.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch
        {
            if (savedPath is not null)
            {
                await storage.DeleteAsync(savedPath, cancellationToken);
            }

            throw;
        }
    }

    public async Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Attachments.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken)
            ?? throw new KeyNotFoundException("المرفق غير موجود.");

        await EnsureEntityInScopeAsync(entity.EntityType, entity.EntityId, cancellationToken);

        if (entity.ScanStatus is AttachmentScanStatus.PendingScan or AttachmentScanStatus.Quarantined or AttachmentScanStatus.Rejected)
        {
            throw new UnauthorizedAccessException("لا يمكن تنزيل المرفق قبل اكتمال الفحص الأمني بنجاح.");
        }

        Stream stream;
        try
        {
            stream = await storage.OpenReadAsync(entity.StoragePath, cancellationToken);
        }
        catch (FileNotFoundException)
        {
            throw new InvalidOperationException("تعذر استرجاع محتوى المرفق.");
        }

        await audit.WriteAsync(new AuditEntry
        {
            Action = "Download",
            Module = "Attachments",
            EntityType = nameof(Attachment),
            EntityId = entity.Id.ToString(),
            IsSensitiveView = entity.Classification >= ClassificationLevel.Confidential,
            NewValues = new { entity.OriginalFileName, entity.Classification, entity.ScanStatus }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return (entity, stream);
    }

    private async Task EnsureEntityInScopeAsync(string entityType, Guid entityId, CancellationToken cancellationToken)
    {
        switch (entityType.ToLowerInvariant())
        {
            case "region":
                if (!await db.Regions.AnyAsync(r => r.Id == entityId, cancellationToken) || !scope.CanAccessRegion(entityId))
                {
                    throw new UnauthorizedAccessException("لا صلاحية على نطاق كيان المرفق.");
                }

                break;
            case "facility":
                if (!await db.Facilities.AnyAsync(f => f.Id == entityId, cancellationToken) || !scope.CanAccessFacility(entityId))
                {
                    throw new UnauthorizedAccessException("لا صلاحية على نطاق كيان المرفق.");
                }

                break;
            case "facilityunit":
                if (!await db.FacilityUnits.AnyAsync(u => u.Id == entityId, cancellationToken) || !scope.CanAccessFacilityUnit(entityId))
                {
                    throw new UnauthorizedAccessException("لا صلاحية على نطاق كيان المرفق.");
                }

                break;
            case "organization":
            case "building":
            case "department":
            case "user":
                if (!scope.HasNationalAccess && !currentUser.HasHeadquartersScope)
                {
                    // Non-HQ entities at org level require national/HQ; buildings resolved via facility later phases.
                    var exists = entityType.Equals("organization", StringComparison.OrdinalIgnoreCase)
                        ? await db.Organizations.AnyAsync(o => o.Id == entityId, cancellationToken)
                        : entityType.Equals("user", StringComparison.OrdinalIgnoreCase)
                            ? await db.Users.AnyAsync(u => u.Id == entityId, cancellationToken)
                            : entityType.Equals("building", StringComparison.OrdinalIgnoreCase)
                                ? await db.Buildings.AnyAsync(b => b.Id == entityId, cancellationToken)
                                : await db.Departments.AnyAsync(d => d.Id == entityId, cancellationToken);
                    if (!exists)
                    {
                        throw new KeyNotFoundException("الكيان غير موجود.");
                    }

                    if (entityType.Equals("building", StringComparison.OrdinalIgnoreCase))
                    {
                        var building = await db.Buildings.FirstAsync(b => b.Id == entityId, cancellationToken);
                        if (!scope.CanAccessFacility(building.FacilityId))
                        {
                            throw new UnauthorizedAccessException("لا صلاحية على نطاق كيان المرفق.");
                        }
                    }
                    else if (!scope.HasNationalAccess && !currentUser.HasHeadquartersScope)
                    {
                        throw new UnauthorizedAccessException("لا صلاحية على نطاق كيان المرفق.");
                    }
                }

                break;
            default:
                throw new InvalidOperationException("نوع الكيان غير مدعوم للمرفقات.");
        }
    }
}
