namespace Baseera.Infrastructure.Attachments;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Application.Forms.Responses;
using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "baseera-attachments");
}

public sealed class LocalFileStorage(IOptions<AttachmentStorageOptions> options) : IFileStorage
{
    private readonly string _root = StoragePathGuard.NormalizeRoot(options.Value.RootPath);

    public async Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var safeName = Path.GetFileName(storedFileName);
        var path = Path.GetFullPath(Path.Combine(_root, safeName));
        StoragePathGuard.EnsureInsideRoot(_root, path);

        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return new StoredFileResult(path);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        var full = Path.GetFullPath(storagePath);
        StoragePathGuard.EnsureInsideRoot(_root, full);

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
        if (StoragePathGuard.IsPathInsideRoot(_root, full) && File.Exists(full))
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
    IAuditService audit,
    IFormResponseAttachmentAccessResolver formResponseAttachmentAccess) : IAttachmentService
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

        if (!AttachmentRules.IsAllowedContentType(request.ContentType))
        {
            throw new InvalidOperationException("نوع الملف غير مسموح.");
        }

        await EnsureEntityAccessAsync(request.EntityType, request.EntityId, FormResponseAttachmentOperation.Upload, cancellationToken);

        AttachmentRules.ValidateMagicBytes(request.Content, request.ContentType, request.OriginalFileName);

        var original = AttachmentRules.SanitizeFileName(request.OriginalFileName);
        var sha = await AttachmentRules.ComputeSha256Async(request.Content, cancellationToken);
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
                Action = request.EntityType.Equals("CorrectiveAction", StringComparison.OrdinalIgnoreCase)
                    ? "CorrectiveActionAttachmentUploaded"
                    : "Upload",
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

        await EnsureEntityAccessAsync(entity.EntityType, entity.EntityId, FormResponseAttachmentOperation.Download, cancellationToken);
        if (string.Equals(entity.EntityType, "FormResponse", StringComparison.OrdinalIgnoreCase)
            && entity.Classification >= ClassificationLevel.Confidential)
        {
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormResponseAttachmentDownloaded",
                Module = "Forms",
                EntityType = "FormResponse",
                EntityId = entity.EntityId.ToString(),
                NewValues = new { AttachmentId = entity.Id, Classification = entity.Classification },
                IsSensitiveView = true
            }, cancellationToken);
        }

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
            Action = entity.EntityType.Equals("CorrectiveAction", StringComparison.OrdinalIgnoreCase)
                ? "CorrectiveActionAttachmentDownloaded"
                : "Download",
            Module = "Attachments",
            EntityType = nameof(Attachment),
            EntityId = entity.Id.ToString(),
            IsSensitiveView = entity.Classification >= ClassificationLevel.Confidential,
            NewValues = new { entity.OriginalFileName, entity.Classification, entity.ScanStatus }
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return (entity, stream);
    }

    public async Task<IReadOnlyList<Attachment>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        if (!AttachmentEntityTypes.IsAllowed(entityType))
        {
            throw new InvalidOperationException("نوع الكيان غير مدعوم للمرفقات.");
        }

        await EnsureEntityAccessAsync(entityType, entityId, FormResponseAttachmentOperation.List, cancellationToken);

        return await db.Attachments
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.UploadedAtUtc)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Anti-enumeration: missing entity and out-of-scope both surface as NotFound (KeyNotFoundException).
    /// Global/HQ callers still must reference a real entity — orphan IDs are rejected.
    /// </summary>
    private async Task EnsureEntityAccessAsync(
        string entityType,
        Guid entityId,
        FormResponseAttachmentOperation formResponseOperation,
        CancellationToken cancellationToken)
    {
        if (string.Equals(entityType, "FormResponse", StringComparison.OrdinalIgnoreCase))
        {
            var decision = await formResponseAttachmentAccess.ResolveAsync(entityId, formResponseOperation, cancellationToken);
            if (!decision.Exists || !decision.Allowed)
            {
                throw new KeyNotFoundException("الكيان غير موجود.");
            }

            return;
        }

        var access = await ResolveEntityAccessAsync(entityType, entityId, cancellationToken);
        if (!access.Exists || !access.InScope)
        {
            throw new KeyNotFoundException("الكيان غير موجود.");
        }
    }

    public Task<(bool Exists, bool InScope)> ResolveEntityAccessAsync(
        string entityType,
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var normalized = entityType.ToLowerInvariant();
        return normalized switch
        {
            "organization" => ResolveOrganizationAccessAsync(entityId, cancellationToken),
            "region" => ResolveRegionAccessAsync(entityId, cancellationToken),
            "facility" => ResolveFacilityAccessAsync(entityId, cancellationToken),
            "facilityunit" => ResolveFacilityUnitAccessAsync(entityId, cancellationToken),
            "building" => ResolveBuildingAccessAsync(entityId, cancellationToken),
            "department" => ResolveDepartmentAccessAsync(entityId, cancellationToken),
            "user" => ResolveUserAccessAsync(entityId, cancellationToken),
            "operationalnote" => ResolveOperationalNoteAccessAsync(entityId, cancellationToken),
            "correctiveaction" => ResolveCorrectiveActionAccessAsync(entityId, cancellationToken),
            "formresponse" => ResolveFormResponseEntityAccessAsync(entityId, cancellationToken),
            _ => throw new InvalidOperationException("نوع الكيان غير مدعوم للمرفقات.")
        };
    }

    private async Task<(bool Exists, bool InScope)> ResolveCorrectiveActionAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var action = await db.CorrectiveActions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(a => a.Id == entityId, cancellationToken);
        if (action is null || action.IsDeleted)
        {
            return (false, false);
        }

        var note = await db.OperationalNotes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == action.OperationalNoteId, cancellationToken);
        if (note is null || note.IsDeleted)
        {
            return (false, false);
        }

        return (true, scope.CanAccess(note));
    }

    private async Task<(bool Exists, bool InScope)> ResolveFormResponseEntityAccessAsync(
        Guid entityId,
        CancellationToken cancellationToken)
    {
        var decision = await formResponseAttachmentAccess.ResolveAsync(
            entityId,
            FormResponseAttachmentOperation.List,
            cancellationToken);
        return (decision.Exists, decision.Allowed);
    }

    private async Task<(bool Exists, bool InScope)> ResolveOperationalNoteAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var note = await db.OperationalNotes.IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.Id == entityId, cancellationToken);
        if (note is null || note.IsDeleted)
        {
            return (false, false);
        }

        return (true, scope.CanAccess(note));
    }

    private async Task<(bool Exists, bool InScope)> ResolveOrganizationAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var org = await db.Organizations.FirstOrDefaultAsync(o => o.Id == entityId, cancellationToken);
        if (org is null)
        {
            return (false, false);
        }

        return (true, scope.HasNationalAccess || currentUser.HasHeadquartersScope);
    }

    private async Task<(bool Exists, bool InScope)> ResolveRegionAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var region = await db.Regions.FirstOrDefaultAsync(r => r.Id == entityId, cancellationToken);
        if (region is null)
        {
            return (false, false);
        }

        return (true, scope.CanAccessRegion(region.Id));
    }

    private async Task<(bool Exists, bool InScope)> ResolveFacilityAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == entityId, cancellationToken);
        if (facility is null)
        {
            return (false, false);
        }

        return (true, scope.CanAccessFacility(facility.Id));
    }

    private async Task<(bool Exists, bool InScope)> ResolveFacilityUnitAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var unit = await db.FacilityUnits.FirstOrDefaultAsync(u => u.Id == entityId, cancellationToken);
        if (unit is null)
        {
            return (false, false);
        }

        return (true, scope.CanAccessFacilityUnit(unit.Id));
    }

    private async Task<(bool Exists, bool InScope)> ResolveBuildingAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var building = await db.Buildings.FirstOrDefaultAsync(b => b.Id == entityId, cancellationToken);
        if (building is null)
        {
            return (false, false);
        }

        return (true, scope.CanAccessFacility(building.FacilityId));
    }

    private async Task<(bool Exists, bool InScope)> ResolveDepartmentAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var dept = await db.Departments.FirstOrDefaultAsync(d => d.Id == entityId, cancellationToken);
        if (dept is null)
        {
            return (false, false);
        }

        return (true, scope.HasNationalAccess || currentUser.HasHeadquartersScope);
    }

    private async Task<(bool Exists, bool InScope)> ResolveUserAccessAsync(Guid entityId, CancellationToken cancellationToken)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Id == entityId && !u.IsDeleted, cancellationToken);
        if (user is null)
        {
            return (false, false);
        }

        // User attachments are national/HQ only in A.1 (no regional user-file sharing yet).
        return (true, scope.HasNationalAccess || currentUser.HasHeadquartersScope);
    }
}
