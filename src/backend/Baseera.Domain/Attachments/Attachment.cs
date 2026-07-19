namespace Baseera.Domain.Attachments;

using Baseera.Domain.Common;

public enum AttachmentScanStatus
{
    PendingScan = 0,
    Clean = 1,
    Quarantined = 2,
    Rejected = 3
}

public enum ClassificationLevel
{
    Internal = 0,
    Restricted = 1,
    Confidential = 2,
    Secret = 3
}

public class Attachment : SoftDeletableEntity
{
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTimeOffset UploadedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;
    public string? UploadReason { get; set; }
    public AttachmentScanStatus ScanStatus { get; set; } = AttachmentScanStatus.PendingScan;
    public string StoragePath { get; set; } = string.Empty;
}
