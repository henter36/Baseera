namespace Baseera.Application.Dashboard;

using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Notes;

public enum OperationalDashboardBreakdownDimension
{
    Region = 0,
    Facility = 1,
    NoteType = 2,
    Severity = 3,
    Status = 4
}

public enum OperationalDashboardPriorityQueue
{
    MostOverdueNotes = 0,
    CriticalUnassignedNotes = 1,
    TopOverdueLocations = 2,
    MostOverdueCorrectiveActions = 3,
    RecentRoutingFailures = 4
}

public sealed record OperationalDashboardQuery
{
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int? PeriodDays { get; set; }
    public Guid? RegionId { get; set; }
    public Guid? FacilityId { get; set; }
    public Guid? FacilityUnitId { get; set; }
    public Guid? NoteTypeId { get; set; }
    public NoteSeverity? Severity { get; set; }
    public NoteStatus? Status { get; set; }
    public OperationalDashboardBreakdownDimension? BreakdownBy { get; set; }
    public OperationalDashboardPriorityQueue? Queue { get; set; }
}

public sealed record OperationalDashboardWorkloadSummaryDto(
    int OpenTotal,
    int Assigned,
    int InProgress,
    int PendingVerification,
    int Reopened,
    int Unassigned,
    int RequiresRouting);

public sealed record OperationalDashboardRiskSummaryDto(
    int Overdue,
    int DueSoon,
    int CriticalOrHigh,
    int OverdueUnassigned,
    int ActiveEscalations,
    int RoutingFailureNoRule,
    int RoutingFailureNoEligibleUser,
    int RoutingFailureInvalidTarget);

public sealed record OperationalDashboardCorrectiveActionsSummaryDto(
    int Active,
    int Overdue,
    int PendingVerification,
    int Reopened,
    int NotesWithStalledActions);

public sealed record OperationalDashboardRoutingSummaryDto(
    int RequiresRouting,
    int FailureNoRule,
    int FailureNoEligibleUser,
    int FailureInvalidTarget);

public sealed record OperationalDashboardSummaryDto(
    OperationalDashboardWorkloadSummaryDto? Workload,
    OperationalDashboardRiskSummaryDto? Risk,
    OperationalDashboardCorrectiveActionsSummaryDto? CorrectiveActions,
    OperationalDashboardRoutingSummaryDto? Routing,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int DueSoonDays);

public sealed record OperationalDashboardTrendPointDto(
    DateTimeOffset BucketStartUtc,
    DateTimeOffset BucketEndUtc,
    string LabelAr,
    int NotesCreated,
    int NotesCompleted,
    int NotesBecameOverdue,
    int CorrectiveActionsCompleted,
    int RoutingSuccess,
    int RoutingFailure);

public sealed record OperationalDashboardTrendsDto(
    IReadOnlyList<OperationalDashboardTrendPointDto> Points,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    string Granularity);

public sealed record OperationalDashboardBreakdownRowDto(
    string Key,
    string LabelAr,
    Guid? EntityId,
    int OpenBurden,
    int Overdue,
    int Critical,
    int Unassigned,
    int CorrectiveActionsOverdue,
    decimal? ClosureRateWithinDue);

public sealed record OperationalDashboardBreakdownsDto(
    OperationalDashboardBreakdownDimension Dimension,
    IReadOnlyList<OperationalDashboardBreakdownRowDto> Rows);

public sealed record OperationalDashboardOverdueNoteQueueItemDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    NoteSeverity Severity,
    string SeverityAr,
    NoteStatus Status,
    string StatusAr,
    DateTimeOffset? DueAtUtc,
    int? OverdueDays,
    Guid? RegionId,
    Guid? FacilityId,
    string? FacilityNameAr);

public sealed record OperationalDashboardOverdueLocationQueueItemDto(
    Guid FacilityId,
    string FacilityNameAr,
    Guid? RegionId,
    string? RegionNameAr,
    int OverdueCount);

public sealed record OperationalDashboardOverdueCorrectiveActionQueueItemDto(
    Guid Id,
    string ReferenceNumber,
    string Title,
    CorrectiveActionStatus Status,
    string StatusAr,
    DateTimeOffset? DueAtUtc,
    int? OverdueDays,
    Guid OperationalNoteId,
    string NoteReferenceNumber);

public sealed record OperationalDashboardRoutingFailureQueueItemDto(
    Guid NoteId,
    string ReferenceNumber,
    string Title,
    string FailureCode,
    string FailureMessageSafe,
    DateTimeOffset DecidedAtUtc);

public sealed record OperationalDashboardPriorityQueuesDto(
    IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>? MostOverdueNotes,
    IReadOnlyList<OperationalDashboardOverdueNoteQueueItemDto>? CriticalUnassignedNotes,
    IReadOnlyList<OperationalDashboardOverdueLocationQueueItemDto>? TopOverdueLocations,
    IReadOnlyList<OperationalDashboardOverdueCorrectiveActionQueueItemDto>? MostOverdueCorrectiveActions,
    IReadOnlyList<OperationalDashboardRoutingFailureQueueItemDto>? RecentRoutingFailures,
    int Limit);
