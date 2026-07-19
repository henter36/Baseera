namespace Baseera.Application.Attachments;

using System.Security.Cryptography;
using Baseera.Application.Abstractions;
using Baseera.Domain.Attachments;

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
    DateTimeOffset UploadedAtUtc);

public interface IAttachmentAppService
{
    Task<AttachmentDto> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default);
    Task<(AttachmentDto Meta, Stream Content)> DownloadAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class AttachmentAppService(IAttachmentService attachments, ICurrentUser currentUser) : IAttachmentAppService
{
    public async Task<AttachmentDto> UploadAsync(UploadAttachmentRequest request, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission("Attachments.Upload"))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية رفع المرفقات.");
        }

        var entity = await attachments.UploadAsync(request, cancellationToken);
        return Map(entity);
    }

    public async Task<(AttachmentDto Meta, Stream Content)> DownloadAsync(Guid id, CancellationToken cancellationToken = default)
    {
        if (!currentUser.HasPermission("Attachments.Download") &&
            !currentUser.HasPermission("Attachments.DownloadSensitive"))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية تنزيل المرفقات.");
        }

        var (entity, stream) = await attachments.DownloadAsync(id, cancellationToken);
        if (entity.Classification >= ClassificationLevel.Confidential &&
            !currentUser.HasPermission("Attachments.DownloadSensitive"))
        {
            await stream.DisposeAsync();
            throw new UnauthorizedAccessException("يتطلب تنزيل هذا المرفق صلاحية حساسة.");
        }

        return (Map(entity), stream);
    }

    private static AttachmentDto Map(Attachment a) => new(
        a.Id, a.EntityType, a.EntityId, a.OriginalFileName, a.ContentType, a.SizeBytes,
        a.Sha256, a.Classification, a.ScanStatus, a.UploadedAtUtc);
}

public static class AttachmentRules
{
    public static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "text/plain",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    public const long MaxSizeBytes = 10 * 1024 * 1024;

    public static string SanitizeFileName(string original)
    {
        var name = Path.GetFileName(original);
        foreach (var c in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(c, '_');
        }

        name = name.Replace("..", "_");
        return string.IsNullOrWhiteSpace(name) ? "file.bin" : name;
    }

    public static string ComputeSha256(Stream stream)
    {
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        var hash = SHA256.HashData(stream);
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }

        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
