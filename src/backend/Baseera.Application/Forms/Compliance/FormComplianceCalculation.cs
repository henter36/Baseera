namespace Baseera.Application.Forms.Compliance;

using Baseera.Domain.Forms;

public interface IFormCompletionTimestampResolver
{
    DateTimeOffset? Resolve(FormCompletionBasis basis, FormResponse? response);
}

public sealed class FormCompletionTimestampResolver : IFormCompletionTimestampResolver
{
    public DateTimeOffset? Resolve(FormCompletionBasis basis, FormResponse? response)
    {
        if (response is null)
        {
            return null;
        }

        return basis switch
        {
            FormCompletionBasis.Submitted => response.SubmittedAtUtc,
            FormCompletionBasis.Approved => response.ApprovedAtUtc,
            _ => null
        };
    }
}

internal sealed class FormComplianceSourceRow
{
    public Guid AssignmentId { get; init; }
    public Guid CampaignId { get; init; }
    public Guid FormDefinitionId { get; init; }
    public Guid CycleId { get; init; }
    public Guid FacilityId { get; init; }
    public Guid RegionIdAtAssignment { get; init; }
    public string FacilityCodeAtAssignment { get; init; } = string.Empty;
    public string FacilityNameAtAssignment { get; init; } = string.Empty;
    public string RegionNameAtAssignment { get; init; } = string.Empty;
    public string CampaignCode { get; init; } = string.Empty;
    public string CampaignNameAr { get; init; } = string.Empty;
    public bool IsAvailable { get; init; }
    public string? UnavailableReason { get; init; }
    public FormCycleStatus CycleStatus { get; init; }
    public FormCompletionBasis CompletionBasis { get; init; }
    public FormResponseStatus? ResponseStatus { get; init; }
    public Guid? ResponseId { get; init; }
    public DateTimeOffset OpenAtUtc { get; init; }
    public DateTimeOffset DueAtUtc { get; init; }
    public DateTimeOffset CloseAtUtc { get; init; }
    public DateTimeOffset ScheduledOccurrenceUtc { get; init; }
    public int SequenceNumber { get; init; }
    public string OccurrenceKey { get; init; } = string.Empty;
    public DateTimeOffset EffectiveDueAtUtc { get; init; }
    public DateTimeOffset? CompletionAtUtc { get; init; }
    public DateTimeOffset? LastSavedAtUtc { get; init; }
    public DateTimeOffset? SubmittedAtUtc { get; init; }
    public Guid? LastSavedByUserId { get; init; }
    public Guid? SubmittedByUserId { get; init; }
    public string? LastSavedByUserName { get; init; }
    public string? SubmittedByUserName { get; init; }
    public bool IsCompleted { get; init; }
    public bool IsOverdue { get; init; }
}

internal sealed record FormCompliancePage(int Page, int PageSize, string? Search);
internal sealed record PreviousCycleLookup(Guid CurrentCycleId, Guid CampaignId, int PreviousSequenceNumber);
internal sealed record ComplianceStatusBucket(
    bool IsAvailable,
    FormResponseStatus? ResponseStatus,
    bool IsCompleted,
    bool IsOverdue,
    int Count);
internal sealed record ComplianceTimingAggregate(
    int CompletedOnTime,
    int CompletedLate,
    double? AverageMinutes,
    int UnknownCompletionTimestamp,
    int InvalidCompletionDuration);

internal sealed record FormComplianceMetricAggregate(
    int TargetedAssignmentCount,
    int DistinctFacilityCount,
    int UnavailableAssignmentCount,
    int EligibleAssignmentCount,
    int CompletedCount,
    int NotStartedCount,
    int DraftCount,
    int SubmittedCount,
    int UnderReviewCount,
    int ReturnedCount,
    int ApprovedCount,
    int RejectedCount,
    int ClosedCount,
    int OverdueCount,
    int CompletedOnTimeCount,
    int CompletedLateCount,
    double? AverageCompletionMinutes,
    int UnknownCompletionTimestampCount,
    int InvalidCompletionDurationCount);

internal static class FormComplianceRates
{
    public static decimal? Rate(int numerator, int denominator) =>
        denominator == 0 ? null : decimal.Divide(numerator * 100m, denominator);

    public static int Remaining(int eligible, int completed) => Math.Max(eligible - completed, 0);
}
