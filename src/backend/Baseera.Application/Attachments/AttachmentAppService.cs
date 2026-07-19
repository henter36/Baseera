namespace Baseera.Application.Attachments;

using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;
using Baseera.Domain.Identity;

public sealed record AttachmentDto(
    Guid Id,
    string EntityType,
    Guid EntityId,
    string OriginalFileName,
    string ContentType,
    long SizeBytes,
    string Sha256,
    ClassificationLevel Classification,
    AttachmentScanStatus ScanStatus,
    DateTimeOffset UploadedAtUtc,
    bool IsSensitiveRedacted = false);

public interface IAttachmentAppService
{
    Task<AttachmentDto> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<(AttachmentDto Meta, Stream Content)> DownloadAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AttachmentDto>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}

public sealed class AttachmentAppService(IAttachmentService attachments, ICurrentUser currentUser) : IAttachmentAppService
{
    public async Task<AttachmentDto> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionCodes.AttachmentsUpload))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية رفع المرفقات.");
        }

        var entity = await attachments.UploadAsync(request, cancellationToken);
        return Map(entity);
    }

    public async Task<(AttachmentDto Meta, Stream Content)> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission(PermissionCodes.AttachmentsDownload) &&
            !currentUser.HasPermission(PermissionCodes.AttachmentsDownloadSensitive))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنزيل المرفقات.");
        }

        var (entity, stream) = await attachments.DownloadAsync(id, cancellationToken);
        if (entity.Classification >= ClassificationLevel.Confidential &&
            !currentUser.HasPermission(PermissionCodes.AttachmentsDownloadSensitive))
        {
            await stream.DisposeAsync();
            throw new UnauthorizedAccessException("يتطلب تنزيل هذا المرفق صلاحية حساسة.");
        }

        return (Map(entity), stream);
    }

    public async Task<IReadOnlyList<AttachmentDto>> ListForEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        var entities = await attachments.ListForEntityAsync(entityType, entityId, cancellationToken);
        var canSensitive = currentUser.HasPermission(PermissionCodes.AttachmentsDownloadSensitive);
        return entities.Select(a => Map(a, RedactSensitiveMetadata(a, canSensitive))).ToList();
    }

    private static bool RedactSensitiveMetadata(Attachment attachment, bool canViewSensitive) =>
        attachment.Classification >= ClassificationLevel.Confidential && !canViewSensitive;

    private static AttachmentDto Map(Attachment a, bool redact = false) => new(
        a.Id,
        a.EntityType,
        a.EntityId,
        redact ? "[محجوب]" : a.OriginalFileName,
        redact ? "application/octet-stream" : a.ContentType,
        redact ? 0 : a.SizeBytes,
        redact ? string.Empty : a.Sha256,
        a.Classification,
        a.ScanStatus,
        a.UploadedAtUtc,
        redact);
}
