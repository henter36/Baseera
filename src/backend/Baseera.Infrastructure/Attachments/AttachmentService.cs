namespace Baseera.Infrastructure.Attachments;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Domain.Attachments;
using Baseera.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

public sealed class AttachmentStorageOptions
{
    public string RootPath { get; set; } = Path.Combine(Path.GetTempPath(), "baseera-attachments");
}

public sealed class LocalFileStorage(IOptions<AttachmentStorageOptions> options) : IFileStorage
{
    private readonly string _root = options.Value.RootPath;

    public async Task<StoredFileResult> SaveAsync(Stream content, string storedFileName, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, storedFileName);
        await using var file = File.Create(path);
        await content.CopyToAsync(file, cancellationToken);
        return new StoredFileResult(path);
    }

    public Task<Stream> OpenReadAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        Stream stream = File.OpenRead(storagePath);
        return Task.FromResult(stream);
    }

    public Task DeleteAsync(string storagePath, CancellationToken cancellationToken = default)
    {
        if (File.Exists(storagePath))
        {
            File.Delete(storagePath);
        }

        return Task.CompletedTask;
    }
}

public sealed class AttachmentService(
    BaseeraDbContext db,
    IFileStorage storage,
    ICurrentUser currentUser,
    IAuditService audit) : IAttachmentService
{
    public async Task<Attachment> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SizeBytes <= 0 || request.SizeBytes > AttachmentRules.MaxSizeBytes)
        {
            throw new InvalidOperationException($"حجم الملف غير مسموح. الحد الأقصى {AttachmentRules.MaxSizeBytes} بايت.");
        }

        if (!AttachmentRules.AllowedContentTypes.Contains(request.ContentType))
        {
            throw new InvalidOperationException("نوع الملف غير مسموح.");
        }

        var original = AttachmentRules.SanitizeFileName(request.OriginalFileName);
        var sha = AttachmentRules.ComputeSha256(request.Content);
        var storedName = $"{Guid.NewGuid():N}_{original}";
        var saved = await storage.SaveAsync(request.Content, storedName, cancellationToken);

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
            ScanStatus = AttachmentScanStatus.Clean,
            StoragePath = saved.StoragePath,
            CreatedBy = currentUser.ExternalSubject
        };

        db.Attachments.Add(entity);
        await db.SaveChangesAsync(cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "Upload",
            Module = "Attachments",
            EntityType = nameof(Attachment),
            EntityId = entity.Id.ToString(),
            NewValues = new { entity.OriginalFileName, entity.ContentType, entity.SizeBytes, entity.Sha256, entity.Classification },
            Reason = request.UploadReason
        }, cancellationToken);

        return entity;
    }

    public async Task<(Attachment Attachment, Stream Content)> DownloadAsync(Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var entity = await db.Attachments.FindAsync([attachmentId], cancellationToken)
            ?? throw new KeyNotFoundException("المرفق غير موجود.");

        if (entity.IsDeleted)
        {
            throw new KeyNotFoundException("المرفق غير موجود.");
        }

        var stream = await storage.OpenReadAsync(entity.StoragePath, cancellationToken);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "Download",
            Module = "Attachments",
            EntityType = nameof(Attachment),
            EntityId = entity.Id.ToString(),
            IsSensitiveView = entity.Classification >= ClassificationLevel.Confidential,
            NewValues = new { entity.OriginalFileName, entity.Classification }
        }, cancellationToken);

        return (entity, stream);
    }
}
