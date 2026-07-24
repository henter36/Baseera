namespace Baseera.Application.Workspaces;

public sealed record FacilityWorkspaceHeaderPayload
{
    public required Guid FacilityId { get; init; }
    public required string FacilityNameAr { get; init; }
    public required Guid RegionId { get; init; }
    public required string RegionNameAr { get; init; }
    public string? FacilityType { get; init; }
    public required DateTimeOffset FromUtc { get; init; }
    public required DateTimeOffset ToUtc { get; init; }
    public required DateTimeOffset CalculatedAtUtc { get; init; }
}

public sealed record FacilityExecutiveSummaryPayload
{
    public required string StatusCode { get; init; }
    public required string StatusAr { get; init; }
    public required int PriorityIssues { get; init; }
    public required string TopDriverAr { get; init; }
    public required string ChangeSummaryAr { get; init; }
    public required string TopPendingActionAr { get; init; }
    public required IReadOnlyList<string> ConfidenceReasons { get; init; }
    public required DateTimeOffset CalculatedAtUtc { get; init; }
}

public sealed record FacilityNotesOverviewPayload(
    int OpenNotes,
    int CriticalNotes,
    int OverdueNotes,
    int UnassignedNotes,
    int RequiresMyAction,
    int NewInPeriod,
    IReadOnlyList<FacilityTopBucketPayload> TopNoteTypes);

public sealed record FacilityCorrectiveActionsPayload(
    int OpenActions,
    int OverdueActions,
    int InProgressActions,
    int PendingVerificationActions,
    int ReopenedActions,
    int CriticalActions,
    double? AverageClosureHours);

public sealed record FacilityAlertsEscalationsPayload(
    int PersonalUnreadNotifications,
    int OpenEscalations,
    int CriticalEscalations,
    int OverdueAlerts,
    DateTimeOffset? LastEscalationProcessedAtUtc,
    int RequiresAcknowledgement);

public sealed record FacilityFormCompliancePayload
{
    public required int TargetedForms { get; init; }
    public required int CompletedForms { get; init; }
    public required int RemainingForms { get; init; }
    public required int OverdueForms { get; init; }
    public decimal? CompletionRate { get; init; }
    public DateTimeOffset? NearestDueAtUtc { get; init; }
    public required int NotStartedForms { get; init; }
    public required int PendingReviewForms { get; init; }
}

public sealed record FacilityPriorityQueuePayload(
    int Limit,
    IReadOnlyList<FacilityPriorityItemPayload> Items);

public sealed record FacilityPriorityItemPayload
{
    public required string Type { get; init; }
    public required string Reference { get; init; }
    public required string TitleAr { get; init; }
    public required string SeverityAr { get; init; }
    public required int PriorityRank { get; init; }
    public required string ReasonAr { get; init; }
    public DateTimeOffset? DueAtUtc { get; init; }
    public int? OverdueDays { get; init; }
    public string? OwnerAr { get; init; }
    public required string ActionLabelAr { get; init; }
    public required DrillDownTarget DrillDownTarget { get; init; }
}

public sealed record FacilityRecentActivityPayload(
    int Limit,
    IReadOnlyList<FacilityActivityItemPayload> Items);

public sealed record FacilityActivityItemPayload
{
    public required string EventType { get; init; }
    public required string TitleAr { get; init; }
    public string? DescriptionAr { get; init; }
    public required DateTimeOffset OccurredAtUtc { get; init; }
    public string? ActorDisplayName { get; init; }
    public required string EntityReference { get; init; }
    public required string Tone { get; init; }
    public required DrillDownTarget DrillDownTarget { get; init; }
}

public sealed record FacilityTopBucketPayload(string LabelAr, int Count);

internal sealed record FacilityWorkspaceFacilityInfo(
    Guid FacilityId,
    string FacilityNameAr,
    Guid RegionId,
    string RegionNameAr,
    string? FacilityType);

internal sealed record FacilityWorkspaceMetrics(
    FacilityWorkspaceFacilityInfo Facility,
    FacilityNotesOverviewPayload Notes,
    FacilityCorrectiveActionsPayload CorrectiveActions,
    FacilityAlertsEscalationsPayload Alerts,
    FacilityFormCompliancePayload FormCompliance);
