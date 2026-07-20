namespace Baseera.Application.CorrectiveActions;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;

public sealed record CorrectiveActionListItemDto(
    Guid Id,
    string ReferenceNumber,
    Guid OperationalNoteId,
    string? OperationalNoteReferenceNumber,
    string Title,
    string? DescriptionSnippet,
    CorrectiveActionPriority Priority,
    string PriorityAr,
    CorrectiveActionStatus Status,
    string StatusAr,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    bool IsDueSoon,
    int? OverdueDays,
    string? CurrentAssigneeDisplay,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record CorrectiveActionDetailDto(
    Guid Id,
    string ReferenceNumber,
    Guid OperationalNoteId,
    string? OperationalNoteReferenceNumber,
    string Title,
    string Description,
    CorrectiveActionPriority Priority,
    string PriorityAr,
    CorrectiveActionStatus Status,
    string StatusAr,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    Guid CreatedByUserId,
    string? CreatedByDisplayName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? WorkStartedAtUtc,
    DateTimeOffset? SubmittedForVerificationAtUtc,
    DateTimeOffset? CompletedAtUtc,
    Guid? CompletedByUserId,
    string? CompletionSummary,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenReason,
    DateTimeOffset? CancelledAtUtc,
    string? CancelReason,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    int? OverdueDays,
    CorrectiveActionAssignmentDto? CurrentAssignment,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record CorrectiveActionAssignmentDto(
    Guid Id,
    Guid CorrectiveActionId,
    Guid? AssignedToUserId,
    string? AssignedToUserDisplayName,
    Guid? AssignedToDepartmentId,
    string? AssignedToDepartmentName,
    Guid AssignedByUserId,
    string? AssignedByDisplayName,
    DateTimeOffset AssignedAtUtc,
    DateTimeOffset? DueAtUtc,
    string Reason,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    DateTimeOffset? EndedAtUtc,
    string? EndReason,
    bool IsCurrent);

public sealed record CorrectiveActionStatusHistoryDto(
    Guid Id,
    CorrectiveActionStatus? FromStatus,
    CorrectiveActionStatus ToStatus,
    string ToStatusAr,
    Guid ChangedByUserId,
    string? ChangedByDisplayName,
    DateTimeOffset ChangedAtUtc,
    string? Reason,
    Guid? AssignmentId,
    string? MetadataJson);

public sealed record CreateCorrectiveActionRequest(
    string Title,
    string Description,
    CorrectiveActionPriority Priority,
    ClassificationLevel? Classification,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc);

public sealed record UpdateCorrectiveActionRequest(
    string Title,
    string Description,
    CorrectiveActionPriority Priority,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc,
    string RowVersion);

public sealed record AssignCorrectiveActionRequest(
    Guid? AssignedToUserId,
    Guid? AssignedToDepartmentId,
    DateTimeOffset? DueAtUtc,
    string Reason,
    string RowVersion);

public sealed record TransitionCorrectiveActionRequest(string Reason, string RowVersion);

public sealed record CompleteCorrectiveActionRequest(string Reason, string CompletionSummary, string RowVersion);

public sealed record ReopenCorrectiveActionRequest(string Reason, string RowVersion);

public sealed record CorrectiveActionListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public Guid? NoteId { get; set; }
    public CorrectiveActionStatus? Status { get; set; }
    public CorrectiveActionPriority? Priority { get; set; }
    public ClassificationLevel? Classification { get; set; }
    public Guid? OwnerDepartmentId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public bool OverdueOnly { get; set; }
    public int? DueSoonDays { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}
