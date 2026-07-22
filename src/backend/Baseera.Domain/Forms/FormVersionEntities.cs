namespace Baseera.Domain.Forms;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum FormVersionStatus
{
    Draft = 0,
    InReview = 1,
    ChangesRequested = 2,
    Rejected = 3,
    Locked = 4
}

public enum FormVersionReviewDecisionType
{
    SubmitForReview = 0,
    RequestChanges = 1,
    ApproveAndLock = 2,
    Reject = 3,
    Reopen = 4
}

public enum FormTemplateVisibility
{
    Organization = 0,
    Department = 1,
    Private = 2
}

public class FormVersion : EntityBase
{
    public Guid FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public int VersionNumber { get; set; }
    public FormVersionStatus Status { get; set; } = FormVersionStatus.Draft;
    public Guid? BasedOnVersionId { get; set; }
    public FormVersion? BasedOnVersion { get; set; }
    public string DraftSchemaJson { get; set; } = "{}";
    public string? DraftSchemaHash { get; set; }
    public int SchemaFormatVersion { get; set; } = 1;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public DateTimeOffset? LastSavedAtUtc { get; set; }
    public DateTimeOffset? SubmittedForReviewAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public Guid? ApprovedByUserId { get; set; }
    public User? ApprovedByUser { get; set; }
    public Guid? SnapshotId { get; set; }
    public FormSchemaSnapshot? Snapshot { get; set; }
    public ICollection<FormVersionReviewDecision> ReviewDecisions { get; set; } = new List<FormVersionReviewDecision>();
}

public sealed record FormSchemaSnapshotData
{
    public required Guid FormVersionId { get; init; }
    public required int SchemaFormatVersion { get; init; }
    public required string CanonicalSchemaJson { get; init; }
    public required string SchemaHash { get; init; }
    public required int SchemaSizeBytes { get; init; }
    public required int PageCount { get; init; }
    public required int SectionCount { get; init; }
    public required int FieldCount { get; init; }
    public required int CalculatedFieldCount { get; init; }
    public required int ConditionCount { get; init; }
    public required Guid CreatedByUserId { get; init; }
    public DateTimeOffset? CreatedAtUtc { get; init; }
}

public class FormSchemaSnapshot
{
    private FormSchemaSnapshot()
    {
    }

    public Guid Id { get; private set; }
    public Guid FormVersionId { get; private set; }
    public FormVersion FormVersion { get; private set; } = null!;
    public int SchemaFormatVersion { get; private set; }
    public string CanonicalSchemaJson { get; private set; } = string.Empty;
    public string SchemaHash { get; private set; } = string.Empty;
    public int SchemaSizeBytes { get; private set; }
    public int PageCount { get; private set; }
    public int SectionCount { get; private set; }
    public int FieldCount { get; private set; }
    public int CalculatedFieldCount { get; private set; }
    public int ConditionCount { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public User CreatedByUser { get; private set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static FormSchemaSnapshot Create(FormSchemaSnapshotData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (data.FormVersionId == Guid.Empty)
        {
            throw new ArgumentException("معرّف الإصدار مطلوب.", nameof(data));
        }

        if (string.IsNullOrWhiteSpace(data.CanonicalSchemaJson))
        {
            throw new ArgumentException("مخطط اللقطة مطلوب.", nameof(data));
        }

        if (string.IsNullOrWhiteSpace(data.SchemaHash) || data.SchemaHash.Length is < 1 or > 64)
        {
            throw new ArgumentException("تجزئة المخطط غير صالحة.", nameof(data));
        }

        if (data.SchemaSizeBytes < 0 || data.PageCount < 0 || data.SectionCount < 0 || data.FieldCount < 0
            || data.CalculatedFieldCount < 0 || data.ConditionCount < 0)
        {
            throw new ArgumentException("عدادات اللقطة غير صالحة.", nameof(data));
        }

        if (data.CreatedByUserId == Guid.Empty)
        {
            throw new ArgumentException("معرّف المنشئ مطلوب.", nameof(data));
        }

        if (data.CreatedAtUtc is DateTimeOffset createdAt && createdAt == default)
        {
            throw new ArgumentException("وقت الإنشاء غير صالح.", nameof(data));
        }

        return new FormSchemaSnapshot
        {
            Id = Guid.NewGuid(),
            FormVersionId = data.FormVersionId,
            SchemaFormatVersion = data.SchemaFormatVersion,
            CanonicalSchemaJson = data.CanonicalSchemaJson,
            SchemaHash = data.SchemaHash,
            SchemaSizeBytes = data.SchemaSizeBytes,
            PageCount = data.PageCount,
            SectionCount = data.SectionCount,
            FieldCount = data.FieldCount,
            CalculatedFieldCount = data.CalculatedFieldCount,
            ConditionCount = data.ConditionCount,
            CreatedByUserId = data.CreatedByUserId,
            CreatedAtUtc = data.CreatedAtUtc ?? DateTimeOffset.UtcNow
        };
    }
}

public class FormVersionReviewDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;
    public FormVersionReviewDecisionType Decision { get; set; }
    public string? Reason { get; set; }
    public Guid ReviewedByUserId { get; set; }
    public User ReviewedByUser { get; set; } = null!;
    public DateTimeOffset ReviewedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public FormVersionStatus FromStatus { get; set; }
    public FormVersionStatus ToStatus { get; set; }
    public bool IsAdministrativeOverride { get; set; }
}

public class FormDefinitionVersionCounter
{
    public Guid FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public int NextVersionNumber { get; set; } = 1;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}

public class FormTemplate : SoftDeletableEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;
    public FormTemplateVisibility Visibility { get; set; } = FormTemplateVisibility.Organization;
    public Guid? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }
    public Guid OwnerUserId { get; set; }
    public User OwnerUser { get; set; } = null!;
    public Guid? SourceFormDefinitionId { get; set; }
    public FormDefinition? SourceFormDefinition { get; set; }
    public Guid? SourceFormVersionId { get; set; }
    public FormVersion? SourceFormVersion { get; set; }
    public int SchemaFormatVersion { get; set; } = 1;
    public string CanonicalSchemaJson { get; set; } = string.Empty;
    public string SchemaHash { get; set; } = string.Empty;
    public int SchemaSizeBytes { get; set; }
    public int PageCount { get; set; }
    public int SectionCount { get; set; }
    public int FieldCount { get; set; }
}
