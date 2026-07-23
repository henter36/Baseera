namespace Baseera.Application.Forms.Compliance;

using Baseera.Application.Common;
using Baseera.Domain.Forms;

public enum FormComplianceTrendGroupBy
{
    Cycle = 0,
    Day = 1
}

public enum FormComplianceExportView
{
    Regions = 0,
    Facilities = 1,
    Cycles = 2,
    Pending = 3
}

public sealed record FormComplianceQuery
{
    public DateTimeOffset? FromUtc { get; init; }
    public DateTimeOffset? ToUtc { get; init; }
    public Guid? FormDefinitionId { get; init; }
    public Guid? CampaignId { get; init; }
    public Guid? CycleId { get; init; }
    public Guid? RegionId { get; init; }
    public Guid? FacilityId { get; init; }
    public FormCycleStatus? CycleStatus { get; init; }
    public FormCompletionBasis? CompletionBasis { get; init; }
    public FormResponseStatus? ResponseStatus { get; init; }
    public bool? IsCompleted { get; init; }
    public bool? IsOverdue { get; init; }
    public bool? IsAvailable { get; init; }
    public string? Search { get; init; }
    public string? Sort { get; init; }
    public int? Page { get; init; }
    public int? PageSize { get; init; }
    public FormComplianceTrendGroupBy? GroupBy { get; init; }
    public FormComplianceExportView? View { get; init; }
}

public sealed record FormComplianceSummaryDto
{
    public required int TargetedAssignmentCount { get; init; }
    public required int DistinctFacilityCount { get; init; }
    public required int UnavailableAssignmentCount { get; init; }
    public required int EligibleAssignmentCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int RemainingCount { get; init; }
    public required decimal? CompletionRate { get; init; }
    public required int NotStartedCount { get; init; }
    public required int DraftCount { get; init; }
    public required int SubmittedCount { get; init; }
    public required int UnderReviewCount { get; init; }
    public required int ReturnedCount { get; init; }
    public required int ApprovedCount { get; init; }
    public required int RejectedCount { get; init; }
    public required int ClosedCount { get; init; }
    public required int OverdueCount { get; init; }
    public required int CompletedOnTimeCount { get; init; }
    public required int CompletedLateCount { get; init; }
    public required double? AverageCompletionMinutes { get; init; }
    public required int UnknownCompletionTimestampCount { get; init; }
    public required int InvalidCompletionDurationCount { get; init; }
    public required int StatusBucketTotal { get; init; }
    public required bool StatusReconciliationValid { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
}

public sealed record FormComplianceRegionRowDto
{
    public required Guid RegionIdAtAssignment { get; init; }
    public required string RegionNameAtAssignment { get; init; }
    public required int TargetedAssignmentCount { get; init; }
    public required int UnavailableAssignmentCount { get; init; }
    public required int EligibleAssignmentCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int RemainingCount { get; init; }
    public required decimal? CompletionRate { get; init; }
    public required int OverdueCount { get; init; }
    public required int NotStartedCount { get; init; }
    public required int ReturnedCount { get; init; }
    public required double? AverageCompletionMinutes { get; init; }
    public required int Rank { get; init; }
}

public sealed record FormComplianceFacilityRowDto
{
    public required Guid FacilityId { get; init; }
    public required string FacilityCodeAtAssignment { get; init; }
    public required string FacilityNameAtAssignment { get; init; }
    public required Guid RegionIdAtAssignment { get; init; }
    public required string RegionNameAtAssignment { get; init; }
    public required int CycleCount { get; init; }
    public required int EligibleAssignmentCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int RemainingCount { get; init; }
    public required decimal? CompletionRate { get; init; }
    public required int OverdueCount { get; init; }
    public required DateTimeOffset? LatestEffectiveDueAtUtc { get; init; }
    public required Guid? ResponsibleUserId { get; init; }
    public required string? ResponsibleUserName { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
}

public sealed record FormComplianceCycleRowDto
{
    public required Guid CycleId { get; init; }
    public required Guid CampaignId { get; init; }
    public required string CampaignCode { get; init; }
    public required string CampaignNameAr { get; init; }
    public required int SequenceNumber { get; init; }
    public required string OccurrenceKey { get; init; }
    public required DateTimeOffset ScheduledOccurrenceUtc { get; init; }
    public required DateTimeOffset OpenAtUtc { get; init; }
    public required DateTimeOffset DueAtUtc { get; init; }
    public required DateTimeOffset CloseAtUtc { get; init; }
    public required FormCycleStatus CycleStatus { get; init; }
    public required FormCompletionBasis CompletionBasis { get; init; }
    public required int TargetedAssignmentCount { get; init; }
    public required int EligibleAssignmentCount { get; init; }
    public required int CompletedCount { get; init; }
    public required int RemainingCount { get; init; }
    public required decimal? CompletionRate { get; init; }
    public required int OverdueCount { get; init; }
    public required double? AverageCompletionMinutes { get; init; }
    public required decimal? PreviousCycleCompletionRate { get; init; }
    public required decimal? CompletionRateDelta { get; init; }
}

public sealed record FormCompliancePendingItemDto
{
    public required Guid AssignmentId { get; init; }
    public required Guid CampaignId { get; init; }
    public required string CampaignNameAr { get; init; }
    public required Guid CycleId { get; init; }
    public required string OccurrenceKey { get; init; }
    public required Guid FacilityId { get; init; }
    public required string FacilityNameAtAssignment { get; init; }
    public required Guid RegionIdAtAssignment { get; init; }
    public required string RegionNameAtAssignment { get; init; }
    public required Guid? ResponseId { get; init; }
    public required FormResponseStatus? ResponseStatus { get; init; }
    public required FormAssignmentWorkStatus WorkStatus { get; init; }
    public required bool IsOverdue { get; init; }
    public required DateTimeOffset OpenAtUtc { get; init; }
    public required DateTimeOffset EffectiveDueAtUtc { get; init; }
    public required int? DaysOverdue { get; init; }
    public required DateTimeOffset? LastSavedAtUtc { get; init; }
    public required DateTimeOffset? SubmittedAtUtc { get; init; }
    public required Guid? ResponsibleUserId { get; init; }
    public required string? ResponsibleUserName { get; init; }
    public required IReadOnlyList<string> AllowedActions { get; init; }
}

public sealed record FormComplianceTrendPointDto
{
    public required DateTimeOffset? OccurrenceUtc { get; init; }
    public required DateOnly? DateLocal { get; init; }
    public required int EligibleAssignmentCount { get; init; }
    public required int CompletedCount { get; init; }
    public required decimal? CompletionRate { get; init; }
    public required int OverdueCount { get; init; }
    public required double? AverageCompletionMinutes { get; init; }
    public required int? CompletedThatDay { get; init; }
    public required int? CumulativeCompleted { get; init; }
    public required decimal? CumulativeCompletionRate { get; init; }
}

public sealed record FormComplianceExportResult(
    string FileName,
    string ContentType,
    byte[] Content,
    int RowCount);

public interface IFormComplianceQueryService
{
    Task<FormComplianceSummaryDto> GetSummaryAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormComplianceRegionRowDto>> GetRegionsAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormComplianceFacilityRowDto>> GetFacilitiesAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormComplianceCycleRowDto>> GetCyclesAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<FormCompliancePendingItemDto>> GetPendingAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FormComplianceTrendPointDto>> GetTrendAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
    Task<FormComplianceExportResult> ExportCsvAsync(FormComplianceQuery query, CancellationToken cancellationToken = default);
}
