namespace Baseera.Application.Forms.Responses;

using System.Text.Json;
using Baseera.Domain.Attachments;
using Baseera.Domain.Forms;

public sealed record FormCampaignResponsePolicyDto(
    FormCompletionBasis CompletionBasis,
    FormReviewMode ReviewMode,
    int RequiredApprovalLevels,
    bool AllowLateSubmission,
    bool AllowResubmissionAfterReturn,
    bool RequireSubmissionAcknowledgement,
    bool RequireSeparationOfDuties);

public sealed record FormCampaignResponsePolicyRequest(
    FormCompletionBasis CompletionBasis,
    FormReviewMode ReviewMode,
    int RequiredApprovalLevels,
    bool AllowLateSubmission,
    bool AllowResubmissionAfterReturn,
    bool RequireSubmissionAcknowledgement,
    bool RequireSeparationOfDuties);

public sealed record FormResponseWorkspaceItemDto(
    Guid AssignmentId,
    Guid CampaignId,
    string CampaignCode,
    string CampaignNameAr,
    Guid CycleId,
    string OccurrenceKey,
    Guid FacilityId,
    string FacilityNameAr,
    Guid RegionId,
    string RegionNameAr,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset GraceEndsAtUtc,
    DateTimeOffset CloseAtUtc,
    DateTimeOffset EffectiveDueAtUtc,
    Guid? ResponseId,
    FormResponseStatus? ResponseStatus,
    FormAssignmentWorkStatus WorkStatus,
    bool IsOverdue,
    bool IsCompleted,
    int? DraftVersion,
    DateTimeOffset? LastSavedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    int CurrentReviewLevel,
    int RequiredApprovalLevels,
    IReadOnlyList<string> AllowedActions,
    string? RowVersion);

public sealed record FormResponseWorkspacePageDto(
    IReadOnlyList<FormResponseWorkspaceItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);

public sealed record FormResponseValidationIssueDto(
    string Code,
    string Path,
    string? FieldKey,
    string MessageAr,
    string Severity);

public sealed record FormResponseDraftSaveRequest(
    JsonElement Answers,
    Guid ClientMutationId,
    int ExpectedDraftVersion,
    string? RowVersion);

public sealed record FormResponseDraftSaveResultDto(
    Guid ResponseId,
    int DraftVersion,
    string RowVersion,
    DateTimeOffset LastSavedAtUtc,
    IReadOnlyList<FormResponseValidationIssueDto> ValidationIssues,
    IReadOnlyDictionary<string, object?> CalculatedValues,
    IReadOnlyList<string> VisibleFieldKeys,
    IReadOnlyList<string> RequiredFieldKeys);

public sealed record FormResponseValidateRequest(JsonElement Answers);

public sealed record FormResponseValidationResultDto(
    bool IsValid,
    IReadOnlyList<FormResponseValidationIssueDto> Issues,
    string CanonicalAnswersJson,
    string AnswersHash,
    IReadOnlyDictionary<string, object?> CalculatedValues,
    IReadOnlyList<string> VisibleFieldKeys,
    IReadOnlyList<string> RequiredFieldKeys);

public sealed record FormResponseSubmitRequest(
    JsonElement Answers,
    Guid ClientMutationId,
    int ExpectedDraftVersion,
    string RowVersion,
    bool Acknowledged,
    string? AcknowledgementText);

public sealed record FormResponseSubmitResultDto(
    Guid ResponseId,
    Guid SubmissionId,
    int SubmissionNumber,
    FormResponseStatus Status,
    string RowVersion,
    DateTimeOffset SubmittedAtUtc);

public sealed record FormResponseWorkspaceDetailDto(
    Guid AssignmentId,
    Guid CampaignId,
    string CampaignCode,
    string CampaignNameAr,
    Guid CycleId,
    string OccurrenceKey,
    Guid FacilityId,
    string FacilityNameAr,
    Guid RegionId,
    string RegionNameAr,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset GraceEndsAtUtc,
    DateTimeOffset CloseAtUtc,
    DateTimeOffset EffectiveDueAtUtc,
    FormCycleStatus CycleStatus,
    bool AssignmentAvailable,
    string? UnavailableReason,
    Guid? ResponseId,
    FormResponseStatus? ResponseStatus,
    FormAssignmentWorkStatus WorkStatus,
    bool IsOverdue,
    bool IsCompleted,
    int DraftVersion,
    string? DraftAnswersJson,
    string SchemaJson,
    string SchemaHash,
    ClassificationLevel FormClassification,
    FormCampaignResponsePolicyDto Policy,
    FormResponseSubmissionDto? LatestSubmission,
    IReadOnlyList<FormResponseReviewCommentDto> VisibleComments,
    FormResponseValidationResultDto? Validation,
    IReadOnlyList<string> AllowedActions,
    string? RowVersion,
    IReadOnlyDictionary<string, bool> FieldVisibility,
    IReadOnlyDictionary<string, bool> FieldRedacted);

public sealed record FormResponseSubmissionDto(
    Guid Id,
    int SubmissionNumber,
    string CanonicalAnswersJson,
    string AnswersHash,
    Guid SubmittedByUserId,
    DateTimeOffset SubmittedAtUtc,
    bool WasLateAtSubmission,
    DateTimeOffset EffectiveDueAtSubmissionUtc,
    bool Acknowledged);

public sealed record FormResponseReviewCommentDto(
    Guid Id,
    Guid SubmissionId,
    Guid? ReviewDecisionId,
    string? FieldKey,
    string Body,
    bool IsVisibleToRespondent,
    Guid CreatedByUserId,
    DateTimeOffset CreatedAtUtc);

public sealed record FormResponseReviewInboxItemDto(
    Guid ResponseId,
    Guid AssignmentId,
    Guid CampaignId,
    string CampaignCode,
    string CampaignNameAr,
    Guid CycleId,
    string OccurrenceKey,
    Guid FacilityId,
    string FacilityNameAr,
    Guid RegionId,
    string RegionNameAr,
    FormResponseStatus Status,
    int CurrentReviewLevel,
    int RequiredApprovalLevels,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset EffectiveDueAtUtc,
    bool IsOverdue,
    IReadOnlyList<string> AllowedActions,
    string RowVersion);

public sealed record FormResponseReviewDetailDto(
    FormResponseWorkspaceDetailDto Workspace,
    IReadOnlyList<FormResponseSubmissionDto> Submissions,
    IReadOnlyList<FormResponseReviewDecisionDto> Decisions,
    IReadOnlyList<FormResponseReviewCommentDto> Comments,
    IReadOnlyList<FormResponseHistoryDto> History);

public sealed record FormResponseReviewDecisionDto(
    Guid Id,
    Guid SubmissionId,
    int ReviewLevel,
    FormResponseReviewDecisionType Decision,
    string? Reason,
    DateTimeOffset? NewDueAtUtc,
    Guid ReviewedByUserId,
    DateTimeOffset ReviewedAtUtc,
    FormResponseStatus FromStatus,
    FormResponseStatus ToStatus);

public sealed record FormResponseHistoryDto(
    Guid Id,
    string EventType,
    FormResponseStatus? FromStatus,
    FormResponseStatus? ToStatus,
    int? SubmissionNumber,
    int? ReviewLevel,
    string? Reason,
    Guid ActorUserId,
    DateTimeOffset OccurredAtUtc);

public sealed record FormResponseReviewCommentRequest(
    string? FieldKey,
    string Body,
    bool IsVisibleToRespondent);

public sealed record FormResponseReturnRequest(
    string Reason,
    DateTimeOffset? NewDueAtUtc,
    IReadOnlyList<FormResponseReviewCommentRequest>? Comments,
    string RowVersion);

public sealed record FormResponseApproveRequest(
    string? Reason,
    IReadOnlyList<FormResponseReviewCommentRequest>? Comments,
    string RowVersion);

public sealed record FormResponseRejectRequest(
    string Reason,
    IReadOnlyList<FormResponseReviewCommentRequest>? Comments,
    string RowVersion);

public sealed record FormResponseCloseRequest(string? Reason, string RowVersion);

public sealed record FormResponseConflictDto(
    int CurrentDraftVersion,
    string CurrentRowVersion,
    DateTimeOffset? LastSavedAtUtc,
    string ConflictCode);


/// <summary>
/// Workspace list query. Page/PageSize are nullable for Minimal API [AsParameters]
/// binding (missing keys must not 400); Normalize applies defaults page=1, pageSize=20.
/// </summary>
public sealed record FormResponseWorkspaceQuery
{
    public string? WorkStatus { get; init; }
    public Guid? CampaignId { get; init; }
    public Guid? CycleId { get; init; }
    public Guid? FacilityId { get; init; }
    public Guid? RegionId { get; init; }
    public DateTimeOffset? DueFrom { get; init; }
    public DateTimeOffset? DueTo { get; init; }
    public string? Search { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public string? Sort { get; init; }
}

/// <summary>
/// Review inbox query. Page/PageSize nullable for [AsParameters]; Normalize applies defaults.
/// </summary>
public sealed record FormResponseReviewInboxQuery
{
    public string? Status { get; init; }
    public Guid? CampaignId { get; init; }
    public Guid? CycleId { get; init; }
    public Guid? RegionId { get; init; }
    public Guid? FacilityId { get; init; }
    public int? ReviewLevel { get; init; }
    public DateTimeOffset? SubmittedFrom { get; init; }
    public DateTimeOffset? SubmittedTo { get; init; }
    public bool? Overdue { get; init; }
    public string? Search { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
}
