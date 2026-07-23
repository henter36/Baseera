namespace Baseera.Domain.Forms;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;

public enum FormDefinitionStatus
{
    Draft = 0,
    InReview = 1,
    ChangesRequested = 2,
    Approved = 3,
    Rejected = 4,
    Archived = 5
}

public enum FormReviewDecisionType
{
    SubmitForReview = 0,
    RequestChanges = 1,
    Approve = 2,
    Reject = 3,
    Archive = 4,
    Restore = 5
}

public enum FormAccessCapability
{
    View = 0,
    Design = 1,
    Review = 2,
    Approve = 3,
    Archive = 4,
    Restore = 5,
    ViewSensitive = 6,
    ManageAccess = 7,
    ManageRetention = 8,
    Publish = 9,
    Respond = 10,
    ViewResponses = 11,
    ReviewResponses = 12,
    ApproveResponses = 13
}

public enum FormAccessGrantEffect
{
    Allow = 0,
    Deny = 1
}

public enum FormAccessGrantPrincipalType
{
    User = 0,
    Role = 1
}

public class FormDefinition : SoftDeletableEntity, IScopedEntity
{
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string? NameEn { get; set; }
    public string Description { get; set; } = string.Empty;
    public Guid? OwnerDepartmentId { get; set; }
    public Department? OwnerDepartment { get; set; }
    public ClassificationLevel Classification { get; set; } = ClassificationLevel.Internal;
    public ScopeType ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public FacilityUnit? FacilityUnit { get; set; }
    public FormDefinitionStatus Status { get; set; } = FormDefinitionStatus.Draft;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
    public DateTimeOffset? SubmittedForReviewAtUtc { get; set; }
    public DateTimeOffset? ApprovedAtUtc { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public Guid? ArchivedByUserId { get; set; }
    public User? ArchivedByUser { get; set; }
    public Guid? DeletedByUserId { get; set; }
    public User? DeletedByUser { get; set; }
    public Guid? LastModifiedByUserId { get; set; }
    public User? LastModifiedByUser { get; set; }
    public Guid? CurrentLockedVersionId { get; set; }
    public FormVersion? CurrentLockedVersion { get; set; }
    public ICollection<FormReviewDecision> ReviewDecisions { get; set; } = new List<FormReviewDecision>();
    public ICollection<FormAccessGrant> AccessGrants { get; set; } = new List<FormAccessGrant>();
    public ICollection<FormVersion> Versions { get; set; } = new List<FormVersion>();
}

public class FormReviewDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public FormReviewDecisionType Decision { get; set; }
    public string? Reason { get; set; }
    public Guid ReviewedByUserId { get; set; }
    public User ReviewedByUser { get; set; } = null!;
    public DateTimeOffset ReviewedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public FormDefinitionStatus FromStatus { get; set; }
    public FormDefinitionStatus ToStatus { get; set; }
    public bool IsAdministrativeOverride { get; set; }
}

public class FormGovernancePolicy : EntityBase
{
    public bool RequireReviewBeforeApproval { get; set; } = true;
    public bool RequireSeparationOfDuties { get; set; } = true;
    public bool AllowDesignerToReviewOwnForm { get; set; }
    public bool AllowReviewerToApproveOwnReview { get; set; }
    public bool AllowApproverToPublish { get; set; } = true;
    public int DefaultRetentionDays { get; set; } = 365;
    public int SensitiveRetentionDays { get; set; } = 730;
    public int MinimumRetentionDays { get; set; } = 30;
    public bool AuditSensitiveViews { get; set; } = true;
    public bool AuditExports { get; set; } = true;
    public bool RequireReasonForArchive { get; set; } = true;
    public Guid? UpdatedByUserId { get; set; }
    public User? UpdatedByUser { get; set; }
}

public class FormAccessGrant : SoftDeletableEntity
{
    public Guid FormDefinitionId { get; set; }
    public FormDefinition FormDefinition { get; set; } = null!;
    public FormAccessGrantPrincipalType PrincipalType { get; set; }
    public Guid PrincipalId { get; set; }
    public FormAccessCapability Capability { get; set; }
    public FormAccessGrantEffect Effect { get; set; } = FormAccessGrantEffect.Allow;
    public ScopeType? ScopeType { get; set; }
    public Guid? RegionId { get; set; }
    public Region? Region { get; set; }
    public Guid? FacilityId { get; set; }
    public Facility? Facility { get; set; }
    /// <summary>
    /// Deterministic, human-inspectable encoding of (ScopeType, RegionId, FacilityId) used to enforce
    /// uniqueness of (form, principal, capability, effect, scope) via a filtered unique index.
    /// </summary>
    public string ScopeKey { get; set; } = string.Empty;
    public DateTimeOffset? ValidFromUtc { get; set; }
    public DateTimeOffset? ValidToUtc { get; set; }
    public string Reason { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public Guid? RevokedByUserId { get; set; }
    public User? RevokedByUser { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public static string BuildScopeKey(ScopeType? scopeType, Guid? regionId, Guid? facilityId) =>
        scopeType switch
        {
            null => "_",
            Baseera.Domain.Common.ScopeType.Global => "G",
            Baseera.Domain.Common.ScopeType.Headquarters => "H",
            Baseera.Domain.Common.ScopeType.Region => $"R:{regionId:N}",
            Baseera.Domain.Common.ScopeType.Facility => $"F:{facilityId:N}",
            _ => "_"
        };
}
