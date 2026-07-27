namespace Baseera.Application.RiskManagement;

using Baseera.Domain.RiskManagement;

public static class RiskTreatmentPlanStateMachine
{
    public static bool CanTransition(TreatmentPlanStatus from, TreatmentPlanStatus to) =>
        (from, to) switch
        {
            (TreatmentPlanStatus.Draft, TreatmentPlanStatus.PendingApproval) => true,
            (TreatmentPlanStatus.Draft, TreatmentPlanStatus.Cancelled) => true,
            (TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Approved) => true,
            (TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Rejected) => true,
            (TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Draft) => true,
            (TreatmentPlanStatus.Approved, TreatmentPlanStatus.InProgress) => true,
            (TreatmentPlanStatus.Approved, TreatmentPlanStatus.Cancelled) => true,
            (TreatmentPlanStatus.InProgress, TreatmentPlanStatus.Blocked) => true,
            (TreatmentPlanStatus.InProgress, TreatmentPlanStatus.Completed) => true,
            (TreatmentPlanStatus.InProgress, TreatmentPlanStatus.Cancelled) => true,
            (TreatmentPlanStatus.Blocked, TreatmentPlanStatus.InProgress) => true,
            (TreatmentPlanStatus.Blocked, TreatmentPlanStatus.Cancelled) => true,
            _ => false
        };

    public static void EnsureAllowed(TreatmentPlanStatus from, TreatmentPlanStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"انتقال حالة خطة المعالجة غير مسموح من {RiskManagementDisplay.TreatmentPlanStatusAr(from)} إلى {RiskManagementDisplay.TreatmentPlanStatusAr(to)}.");
        }
    }

    /// <summary>Overdue is never a stored transition target — it's an effective/derived status surfaced to read models only.</summary>
    public static bool IsOverdue(TreatmentPlanStatus status, DateTimeOffset dueAtUtc, DateTimeOffset utcNow) =>
        dueAtUtc < utcNow &&
        status is not TreatmentPlanStatus.Completed and not TreatmentPlanStatus.Cancelled and not TreatmentPlanStatus.Rejected;

    public static bool CanClose(TreatmentPlanStatus status) =>
        status is TreatmentPlanStatus.Completed or TreatmentPlanStatus.Cancelled or TreatmentPlanStatus.Rejected;
}

public static class RiskTreatmentActionStateMachine
{
    public static bool CanTransition(RiskTreatmentActionStatus from, RiskTreatmentActionStatus to) =>
        (from, to) switch
        {
            (RiskTreatmentActionStatus.Draft, RiskTreatmentActionStatus.Assigned) => true,
            (RiskTreatmentActionStatus.Draft, RiskTreatmentActionStatus.Cancelled) => true,
            (RiskTreatmentActionStatus.Assigned, RiskTreatmentActionStatus.InProgress) => true,
            (RiskTreatmentActionStatus.Assigned, RiskTreatmentActionStatus.Cancelled) => true,
            (RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.Blocked) => true,
            (RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.PendingVerification) => true,
            (RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.Cancelled) => true,
            (RiskTreatmentActionStatus.Blocked, RiskTreatmentActionStatus.InProgress) => true,
            (RiskTreatmentActionStatus.Blocked, RiskTreatmentActionStatus.Cancelled) => true,
            (RiskTreatmentActionStatus.PendingVerification, RiskTreatmentActionStatus.Completed) => true,
            (RiskTreatmentActionStatus.PendingVerification, RiskTreatmentActionStatus.InProgress) => true,
            (RiskTreatmentActionStatus.PendingVerification, RiskTreatmentActionStatus.Cancelled) => true,
            _ => false
        };

    public static void EnsureAllowed(RiskTreatmentActionStatus from, RiskTreatmentActionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"انتقال حالة إجراء المعالجة غير مسموح من {RiskManagementDisplay.TreatmentActionStatusAr(from)} إلى {RiskManagementDisplay.TreatmentActionStatusAr(to)}.");
        }
    }

    public static bool IsOverdue(RiskTreatmentActionStatus status, DateTimeOffset dueAtUtc, DateTimeOffset utcNow) =>
        dueAtUtc < utcNow &&
        status is not RiskTreatmentActionStatus.Completed and not RiskTreatmentActionStatus.Cancelled;
}
