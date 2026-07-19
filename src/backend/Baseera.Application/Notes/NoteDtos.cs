namespace Baseera.Application.Notes;

using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;

public sealed record NoteListItemDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string? DescriptionSnippet,
    NoteStatus Status,
    string StatusAr,
    NoteSeverity Severity,
    string SeverityAr,
    NoteCategory Category,
    string CategoryAr,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    string? CurrentAssigneeDisplay,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record NoteDetailDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    string Description,
    NoteStatus Status,
    string StatusAr,
    NoteSeverity Severity,
    string SeverityAr,
    NoteCategory Category,
    string CategoryAr,
    NoteSourceType SourceType,
    string SourceAr,
    string? SourceReference,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    Guid ReportedByUserId,
    string? ReportedByDisplayName,
    DateTimeOffset ReportedAtUtc,
    DateTimeOffset? DueAtUtc,
    bool IsOverdue,
    DateTimeOffset? SubmittedAtUtc,
    DateTimeOffset? WorkStartedAtUtc,
    DateTimeOffset? SubmittedForVerificationAtUtc,
    DateTimeOffset? ClosedAtUtc,
    Guid? ClosedByUserId,
    string? ClosureSummary,
    DateTimeOffset? ReopenedAtUtc,
    string? ReopenReason,
    NoteAssignmentDto? CurrentAssignment,
    DateTimeOffset CreatedAtUtc,
    string RowVersion,
    bool IsSensitiveRedacted);

public sealed record NoteAssignmentDto(
    Guid Id,
    Guid OperationalNoteId,
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

public sealed record NoteStatusHistoryDto(
    Guid Id,
    NoteStatus? FromStatus,
    NoteStatus ToStatus,
    string ToStatusAr,
    Guid ChangedByUserId,
    string? ChangedByDisplayName,
    DateTimeOffset ChangedAtUtc,
    string? Reason,
    Guid? AssignmentId);

public sealed record CreateNoteRequest(
    string Title,
    string Description,
    NoteCategory Category,
    NoteSeverity Severity,
    NoteSourceType SourceType,
    string? SourceReference,
    ClassificationLevel Classification,
    ScopeType ScopeType,
    Guid? RegionId,
    Guid? FacilityId,
    Guid? FacilityUnitId,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc);

public sealed record UpdateNoteRequest(
    string Title,
    string Description,
    NoteCategory Category,
    NoteSeverity Severity,
    NoteSourceType SourceType,
    string? SourceReference,
    ClassificationLevel Classification,
    Guid? OwnerDepartmentId,
    DateTimeOffset? DueAtUtc,
    string RowVersion);

public sealed record AssignNoteRequest(
    Guid? AssignedToUserId,
    Guid? AssignedToDepartmentId,
    DateTimeOffset? DueAtUtc,
    string Reason,
    string RowVersion);

public sealed record TransitionNoteRequest(string Reason, string RowVersion);

public sealed record WorkflowActionRequest(string? Reason, string RowVersion);

public sealed record CloseNoteRequest(string Reason, string ClosureSummary, string RowVersion);

public sealed record ReopenNoteRequest(string Reason, string RowVersion);

public sealed record NoteListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Search { get; set; }
    public NoteStatus? Status { get; set; }
    public NoteSeverity? Severity { get; set; }
    public NoteCategory? Category { get; set; }
    public NoteSourceType? SourceType { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public Guid? OwnerDepartmentId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public bool OverdueOnly { get; set; }
    public DateTimeOffset? DueFrom { get; set; }
    public DateTimeOffset? DueTo { get; set; }
    public DateTimeOffset? CreatedFrom { get; set; }
    public DateTimeOffset? CreatedTo { get; set; }
    public string? SortBy { get; set; }
    public bool SortDesc { get; set; }
}
