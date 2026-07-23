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
    Facilities = 0,
    Cycles = 1,
    Pending = 2
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

public sealed record FormComplianceRegionRowDto(
    Guid RegionIdAtAssignment,
    string RegionNameAtAssignment,
    int TargetedAssignmentCount,
    int UnavailableAssignmentCount,
    int EligibleAssignmentCount,
    int CompletedCount,
    int RemainingCount,
    decimal? CompletionRate,
    int OverdueCount,
    int NotStartedCount,
    int ReturnedCount,
    double? AverageCompletionMinutes,
    int Rank);

public sealed record FormComplianceFacilityRowDto(
    Guid FacilityId,
    string FacilityCodeAtAssignment,
    string FacilityNameAtAssignment,
    Guid RegionIdAtAssignment,
    string RegionNameAtAssignment,
    int CycleCount,
    int EligibleAssignmentCount,
    int CompletedCount,
    int RemainingCount,
    decimal? CompletionRate,
    int OverdueCount,
    DateTimeOffset? LatestEffectiveDueAtUtc,
    Guid? ResponsibleUserId,
    string? ResponsibleUserName,
    IReadOnlyList<string> AllowedActions);

public sealed record FormComplianceCycleRowDto(
    Guid CycleId,
    Guid CampaignId,
    string CampaignCode,
    string CampaignNameAr,
    int SequenceNumber,
    string OccurrenceKey,
    DateTimeOffset ScheduledOccurrenceUtc,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset DueAtUtc,
    DateTimeOffset CloseAtUtc,
    FormCycleStatus CycleStatus,
    FormCompletionBasis CompletionBasis,
    int TargetedAssignmentCount,
    int EligibleAssignmentCount,
    int CompletedCount,
    int RemainingCount,
    decimal? CompletionRate,
    int OverdueCount,
    double? AverageCompletionMinutes,
    decimal? PreviousCycleCompletionRate,
    decimal? CompletionRateDelta);

public sealed record FormCompliancePendingItemDto(
    Guid AssignmentId,
    Guid CampaignId,
    string CampaignNameAr,
    Guid CycleId,
    string OccurrenceKey,
    Guid FacilityId,
    string FacilityNameAtAssignment,
    Guid RegionIdAtAssignment,
    string RegionNameAtAssignment,
    Guid? ResponseId,
    FormResponseStatus? ResponseStatus,
    FormAssignmentWorkStatus WorkStatus,
    bool IsOverdue,
    DateTimeOffset OpenAtUtc,
    DateTimeOffset EffectiveDueAtUtc,
    int? DaysOverdue,
    DateTimeOffset? LastSavedAtUtc,
    DateTimeOffset? SubmittedAtUtc,
    Guid? ResponsibleUserId,
    string? ResponsibleUserName,
    IReadOnlyList<string> AllowedActions);

public sealed record FormComplianceTrendPointDto(
    DateTimeOffset? OccurrenceUtc,
    DateOnly? DateLocal,
    int EligibleAssignmentCount,
    int CompletedCount,
    decimal? CompletionRate,
    int OverdueCount,
    double? AverageCompletionMinutes,
    int? CompletedThatDay,
    int? CumulativeCompleted,
    decimal? CumulativeCompletionRate);

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
