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
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormVersionId { get; set; }
    public FormVersion FormVersion { get; set; } = null!;
    public int SchemaFormatVersion { get; set; }
    public string CanonicalSchemaJson { get; set; } = string.Empty;
    public string SchemaHash { get; set; } = string.Empty;
    public int SchemaSizeBytes { get; set; }
    public int PageCount { get; set; }
    public int SectionCount { get; set; }
    public int FieldCount { get; set; }
    public int CalculatedFieldCount { get; set; }
    public int ConditionCount { get; set; }
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
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
