namespace Baseera.Application.Forms;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;

public sealed record FormListItemDto(
    Guid Id,
    string Code,
    string NameAr,
    string? NameEn,
    string? DescriptionSnippet,
    FormDefinitionStatus Status,
    string StatusAr,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record FormDetailDto(
    Guid Id,
    string Code,
    string NameAr,
    string? NameEn,
    string Description,
    FormDefinitionStatus Status,
    string StatusAr,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    Guid CreatedByUserId,
    string? CreatedByDisplayName,
    Guid? UpdatedByUserId,
    string? UpdatedByDisplayName,
    Guid? LastModifiedByUserId,
    string? LastModifiedByDisplayName,
    DateTimeOffset? SubmittedForReviewAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    DateTimeOffset? ArchivedAtUtc,
    Guid? ArchivedByUserId,
    string? ArchivedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? UpdatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted,
    IReadOnlyList<string> AllowedActions);

public sealed record FormReviewDecisionDto(
    Guid Id,
    FormReviewDecisionType Decision,
    string DecisionAr,
    string? Reason,
    Guid ReviewedByUserId,
    string? ReviewedByDisplayName,
    DateTimeOffset ReviewedAtUtc,
    FormDefinitionStatus FromStatus,
    string FromStatusAr,
    FormDefinitionStatus ToStatus,
    string ToStatusAr,
    bool IsAdministrativeOverride);

public sealed record FormAccessGrantDto(
    Guid Id,
    FormAccessGrantPrincipalType PrincipalType,
    Guid PrincipalId,
    string? PrincipalDisplayName,
    FormAccessCapability Capability,
    string CapabilityAr,
    FormAccessGrantEffect Effect,
    ScopeType? ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    string Reason,
    Guid CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    string RowVersion);

public sealed record CreateFormAccessGrantRequest(
    FormAccessGrantPrincipalType PrincipalType,
    Guid PrincipalId,
    FormAccessCapability Capability,
    FormAccessGrantEffect Effect,
    ScopeType? ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    DateTimeOffset? ValidFromUtc,
    DateTimeOffset? ValidToUtc,
    string Reason);

public sealed record FormGovernancePolicyDto(
    Guid Id,
    bool RequireReviewBeforeApproval,
    bool RequireSeparationOfDuties,
    bool AllowDesignerToReviewOwnForm,
    bool AllowReviewerToApproveOwnReview,
    bool AllowApproverToPublish,
    int DefaultRetentionDays,
    int SensitiveRetentionDays,
    int MinimumRetentionDays,
    bool AuditSensitiveViews,
    bool AuditExports,
    bool RequireReasonForArchive,
    string RowVersion);

public sealed record UpdateFormGovernancePolicyRequest(
    bool RequireReviewBeforeApproval,
    bool RequireSeparationOfDuties,
    bool AllowDesignerToReviewOwnForm,
    bool AllowReviewerToApproveOwnReview,
    bool AllowApproverToPublish,
    int DefaultRetentionDays,
    int SensitiveRetentionDays,
    int MinimumRetentionDays,
    bool AuditSensitiveViews,
    bool AuditExports,
    bool RequireReasonForArchive,
    string RowVersion);

public sealed record FormRetentionStatusDto(
    Guid FormDefinitionId,
    bool IsRetentionApplicable,
    DateTimeOffset? RetentionAnchorUtc,
    int RetentionDays,
    DateTimeOffset? ExpiresAtUtc,
    bool IsExpired,
    bool IsEligibleForArchive);

public sealed record CreateFormRequest(
    string Code,
    string NameAr,
    string? NameEn,
    string Description,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId);

public sealed record UpdateFormRequest(
    string NameAr,
    string? NameEn,
    string Description,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    string RowVersion);

public sealed record FormTransitionRequest(string Reason, string RowVersion);

public sealed class FormListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public FormDefinitionStatus? Status { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}
