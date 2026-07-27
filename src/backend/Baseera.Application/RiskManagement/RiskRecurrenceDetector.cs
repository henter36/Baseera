namespace Baseera.Application.RiskManagement;

using Baseera.Domain.RiskManagement;

public enum RecurrencePatternKind
{
    None = 0,
    PotentialDuplicate = 1,
    RecurringPattern = 2
}

/// <summary>Builds the normalized key used to group risks that likely describe the same underlying condition. Never used to auto-merge — only to flag for human review.</summary>
public static class RiskRecurrenceKeyBuilder
{
    public static string Build(string categoryCode, Guid? facilityId, string title)
    {
        var normalizedCategory = (categoryCode ?? string.Empty).Trim().ToUpperInvariant();
        var scopeSegment = facilityId?.ToString() ?? "-";
        var normalizedTitle = NormalizeTitle(title);
        return $"{normalizedCategory}|{scopeSegment}|{normalizedTitle}";
    }

    private static string NormalizeTitle(string title)
    {
        var upper = (title ?? string.Empty).Trim().ToUpperInvariant();
        var parts = upper.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}

/// <summary>
/// Deterministic recurrence/duplicate detection based on shared RecurrenceKey within the same facility.
/// Never merges risks automatically — only classifies the pattern so the UI can surface a "suggested review".
/// </summary>
public static class RiskRecurrenceDetector
{
    private static readonly HashSet<RiskStatus> OpenStatuses =
    [
        RiskStatus.Draft,
        RiskStatus.UnderAssessment,
        RiskStatus.PendingReview,
        RiskStatus.Active,
        RiskStatus.UnderTreatment,
        RiskStatus.Monitoring,
        RiskStatus.PendingAcceptance,
        RiskStatus.PendingClosure
    ];

    public static RecurrencePatternKind Detect(IReadOnlyList<RiskStatus> otherRiskStatusesWithSameKey, int recurrenceThreshold = 2)
    {
        if (otherRiskStatusesWithSameKey.Count == 0)
        {
            return RecurrencePatternKind.None;
        }

        if (otherRiskStatusesWithSameKey.Any(OpenStatuses.Contains))
        {
            return RecurrencePatternKind.PotentialDuplicate;
        }

        return otherRiskStatusesWithSameKey.Count >= recurrenceThreshold
            ? RecurrencePatternKind.RecurringPattern
            : RecurrencePatternKind.None;
    }
}
