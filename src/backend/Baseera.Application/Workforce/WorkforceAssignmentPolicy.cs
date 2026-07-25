namespace Baseera.Application.Workforce;

public static class WorkforceAssignmentPolicy
{
    public static bool PeriodsOverlap(
        DateTimeOffset existingFrom,
        DateTimeOffset? existingTo,
        DateTimeOffset candidateFrom,
        DateTimeOffset? candidateTo)
    {
        var existingEnd = existingTo ?? DateTimeOffset.MaxValue;
        var candidateEnd = candidateTo ?? DateTimeOffset.MaxValue;
        return existingFrom < candidateEnd && candidateFrom < existingEnd;
    }

    public static bool HasConflictingPrimaryAssignment(
        IEnumerable<(bool IsPrimary, DateTimeOffset EffectiveFromUtc, DateTimeOffset? EffectiveToUtc)> existing,
        DateTimeOffset candidateFrom,
        DateTimeOffset? candidateTo,
        bool candidateIsPrimary)
    {
        if (!candidateIsPrimary)
        {
            return false;
        }

        return existing.Any(row =>
            row.IsPrimary
            && PeriodsOverlap(row.EffectiveFromUtc, row.EffectiveToUtc, candidateFrom, candidateTo));
    }
}
