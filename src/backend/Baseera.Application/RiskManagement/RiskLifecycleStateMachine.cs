namespace Baseera.Application.RiskManagement;

using Baseera.Domain.RiskManagement;

/// <summary>
/// RiskRecord.Status transitions. Mirrors CorrectiveActionStateMachine's shape: a pure lookup table plus
/// a throwing guard, so every command service enforces the exact same lifecycle regardless of entry point.
///
/// Beyond the transitions explicitly named in the Phase D.6 spec, this table adds the minimal reverse/reject
/// edges required for the four-eyes review decisions to go somewhere sensible (documented in
/// docs/phase-d6-risk-lifecycle.md):
///   - PendingReview -> UnderAssessment   (assessment review returned/rejected)
///   - PendingAcceptance -> Active        (acceptance request rejected)
///   - Accepted -> PendingReview          (acceptance window expired or periodic review triggered)
///   - Monitoring -> UnderTreatment       (new evidence surfaces during monitoring)
///   - PendingClosure -> Monitoring       (closure request returned/rejected)
///   - Reopened -> UnderAssessment        (a reopened risk must be re-assessed before it can be active again)
/// Archived is only reachable from Closed, by design — a risk must be fully resolved before archival.
/// </summary>
public static class RiskLifecycleStateMachine
{
    public static bool CanTransition(RiskStatus from, RiskStatus to) =>
        (from, to) switch
        {
            (RiskStatus.Draft, RiskStatus.UnderAssessment) => true,
            (RiskStatus.UnderAssessment, RiskStatus.PendingReview) => true,
            (RiskStatus.PendingReview, RiskStatus.Active) => true,
            (RiskStatus.PendingReview, RiskStatus.UnderAssessment) => true,
            (RiskStatus.Active, RiskStatus.UnderTreatment) => true,
            (RiskStatus.Active, RiskStatus.Monitoring) => true,
            (RiskStatus.Active, RiskStatus.PendingAcceptance) => true,
            (RiskStatus.UnderTreatment, RiskStatus.Monitoring) => true,
            (RiskStatus.PendingAcceptance, RiskStatus.Accepted) => true,
            (RiskStatus.PendingAcceptance, RiskStatus.Active) => true,
            (RiskStatus.Accepted, RiskStatus.PendingReview) => true,
            (RiskStatus.Monitoring, RiskStatus.PendingClosure) => true,
            (RiskStatus.Monitoring, RiskStatus.UnderTreatment) => true,
            (RiskStatus.PendingClosure, RiskStatus.Closed) => true,
            (RiskStatus.PendingClosure, RiskStatus.Monitoring) => true,
            (RiskStatus.Closed, RiskStatus.Reopened) => true,
            (RiskStatus.Reopened, RiskStatus.UnderAssessment) => true,
            (RiskStatus.Closed, RiskStatus.Archived) => true,
            _ => false
        };

    public static void EnsureAllowed(RiskStatus from, RiskStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"انتقال حالة الخطر غير مسموح من {RiskManagementDisplay.StatusAr(from)} إلى {RiskManagementDisplay.StatusAr(to)}.");
        }
    }

    public static bool IsDeletable(RiskStatus status) =>
        status is RiskStatus.Draft;

    /// <summary>Whether an accepted risk's acceptance window has lapsed and it must fall back into review.</summary>
    public static bool IsAcceptanceExpired(RiskStatus status, DateTimeOffset? acceptedUntilUtc, DateTimeOffset utcNow) =>
        status == RiskStatus.Accepted && acceptedUntilUtc.HasValue && acceptedUntilUtc.Value < utcNow;

    public static bool IsReviewOverdue(DateTimeOffset? nextReviewDueAtUtc, DateTimeOffset utcNow) =>
        nextReviewDueAtUtc.HasValue && nextReviewDueAtUtc.Value < utcNow;
}
