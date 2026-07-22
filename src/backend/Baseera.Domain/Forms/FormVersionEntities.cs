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

    public static FormSchemaSnapshot Create(
        Guid formVersionId,
        int schemaFormatVersion,
        string canonicalSchemaJson,
        string schemaHash,
        int schemaSizeBytes,
        int pageCount,
        int sectionCount,
        int fieldCount,
        int calculatedFieldCount,
        int conditionCount,
        Guid createdByUserId,
        DateTimeOffset? createdAtUtc = null)
    {
        if (formVersionId == Guid.Empty)
        {
            throw new ArgumentException("معرّف الإصدار مطلوب.", nameof(formVersionId));
        }

        if (string.IsNullOrWhiteSpace(canonicalSchemaJson))
        {
            throw new ArgumentException("مخطط اللقطة مطلوب.", nameof(canonicalSchemaJson));
        }

        if (string.IsNullOrWhiteSpace(schemaHash) || schemaHash.Length > 64)
        {
            throw new ArgumentException("تجزئة المخطط غير صالحة.", nameof(schemaHash));
        }

        if (schemaSizeBytes < 0 || pageCount < 0 || sectionCount < 0 || fieldCount < 0
            || calculatedFieldCount < 0 || conditionCount < 0)
        {
            throw new ArgumentException("عدادات اللقطة غير صالحة.");
        }

        if (createdByUserId == Guid.Empty)
        {
            throw new ArgumentException("معرّف المنشئ مطلوب.", nameof(createdByUserId));
        }

        return new FormSchemaSnapshot
        {
            Id = Guid.NewGuid(),
            FormVersionId = formVersionId,
            SchemaFormatVersion = schemaFormatVersion,
            CanonicalSchemaJson = canonicalSchemaJson,
            SchemaHash = schemaHash,
            SchemaSizeBytes = schemaSizeBytes,
            PageCount = pageCount,
            SectionCount = sectionCount,
            FieldCount = fieldCount,
            CalculatedFieldCount = calculatedFieldCount,
            ConditionCount = conditionCount,
            CreatedByUserId = createdByUserId,
            CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow
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
