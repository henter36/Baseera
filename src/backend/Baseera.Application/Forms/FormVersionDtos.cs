namespace Baseera.Application.Forms;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Baseera.Domain.Forms.Schema;

public sealed record FormVersionListItemDto(
    Guid Id,
    Guid FormDefinitionId,
    int VersionNumber,
    FormVersionStatus Status,
    string StatusAr,
    Guid? BasedOnVersionId,
    string? DraftSchemaHash,
    int SchemaFormatVersion,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastSavedAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    Guid? SnapshotId,
    string RowVersion);

public sealed record FormVersionDetailDto(
    Guid Id,
    Guid FormDefinitionId,
    int VersionNumber,
    FormVersionStatus Status,
    string StatusAr,
    Guid? BasedOnVersionId,
    string DraftSchemaJson,
    string? DraftSchemaHash,
    int SchemaFormatVersion,
    Guid CreatedByUserId,
    Guid? UpdatedByUserId,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? LastSavedAtUtc,
    DateTimeOffset? SubmittedForReviewAtUtc,
    DateTimeOffset? ApprovedAtUtc,
    Guid? ApprovedByUserId,
    Guid? SnapshotId,
    string RowVersion,
    IReadOnlyList<string> AllowedActions);

public sealed record FormVersionValidateResultDto(
    bool IsValid,
    string? SchemaHash,
    IReadOnlyList<FormSchemaValidationIssue> Issues,
    int PageCount,
    int SectionCount,
    int FieldCount,
    int CalculatedFieldCount,
    int ConditionCount);

public sealed record FormSchemaSnapshotDto(
    Guid Id,
    Guid FormVersionId,
    int SchemaFormatVersion,
    string CanonicalSchemaJson,
    string SchemaHash,
    int SchemaSizeBytes,
    int PageCount,
    int SectionCount,
    int FieldCount,
    int CalculatedFieldCount,
    int ConditionCount,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record FormVersionReviewDecisionDto(
    Guid Id,
    FormVersionReviewDecisionType Decision,
    string DecisionAr,
    string? Reason,
    Guid ReviewedByUserId,
    DateTimeOffset ReviewedAtUtc,
    FormVersionStatus FromStatus,
    FormVersionStatus ToStatus,
    bool IsAdministrativeOverride);

public sealed record CreateFormVersionRequest(Guid? BasedOnVersionId);

public sealed record SaveFormSchemaRequest(string SchemaJson, string RowVersion);

public sealed record FormVersionTransitionRequest(string? Reason, string RowVersion);

public sealed record FormTemplateListItemDto(
    Guid Id,
    string Code,
    string NameAr,
    string? NameEn,
    string Description,
    string Category,
    ClassificationLevel Classification,
    FormTemplateVisibility Visibility,
    Guid? OwnerDepartmentId,
    string SchemaHash,
    int PageCount,
    int SectionCount,
    int FieldCount,
    DateTimeOffset CreatedAtUtc);

public sealed record CreateFormTemplateRequest(
    Guid FormDefinitionId,
    Guid FormVersionId,
    string Code,
    string NameAr,
    string? NameEn,
    string Description,
    string Category,
    FormTemplateVisibility Visibility,
    Guid? OwnerDepartmentId);

public sealed record CreateFormFromTemplateRequest(
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

public static class FormVersionLabels
{
    public static string StatusAr(FormVersionStatus status) => status switch
    {
        FormVersionStatus.Draft => "مسودة",
        FormVersionStatus.InReview => "قيد المراجعة",
        FormVersionStatus.ChangesRequested => "مطلوب تعديلات",
        FormVersionStatus.Rejected => "مرفوض",
        FormVersionStatus.Locked => "مقفل",
        _ => status.ToString()
    };

    public static string DecisionAr(FormVersionReviewDecisionType decision) => decision switch
    {
        FormVersionReviewDecisionType.SubmitForReview => "إرسال للمراجعة",
        FormVersionReviewDecisionType.RequestChanges => "طلب تعديلات",
        FormVersionReviewDecisionType.ApproveAndLock => "اعتماد وقفل",
        FormVersionReviewDecisionType.Reject => "رفض",
        FormVersionReviewDecisionType.Reopen => "إعادة فتح",
        _ => decision.ToString()
    };
}
