namespace Baseera.Application.CorrectiveActions;

using Baseera.Domain.CorrectiveActions;

public static class CorrectiveActionStateMachine
{
    public static bool CanTransition(CorrectiveActionStatus from, CorrectiveActionStatus to) =>
        (from, to) switch
        {
            (CorrectiveActionStatus.Draft, CorrectiveActionStatus.Open) => true,
            (CorrectiveActionStatus.Draft, CorrectiveActionStatus.Cancelled) => true,
            (CorrectiveActionStatus.Open, CorrectiveActionStatus.Assigned) => true,
            (CorrectiveActionStatus.Open, CorrectiveActionStatus.Cancelled) => true,
            (CorrectiveActionStatus.Assigned, CorrectiveActionStatus.InProgress) => true,
            (CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Assigned) => true,
            (CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Cancelled) => true,
            (CorrectiveActionStatus.InProgress, CorrectiveActionStatus.PendingVerification) => true,
            (CorrectiveActionStatus.InProgress, CorrectiveActionStatus.Cancelled) => true,
            (CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.Completed) => true,
            (CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.InProgress) => true,
            (CorrectiveActionStatus.PendingVerification, CorrectiveActionStatus.Cancelled) => true,
            (CorrectiveActionStatus.Completed, CorrectiveActionStatus.Reopened) => true,
            (CorrectiveActionStatus.Reopened, CorrectiveActionStatus.Assigned) => true,
            (CorrectiveActionStatus.Reopened, CorrectiveActionStatus.InProgress) => true,
            (CorrectiveActionStatus.Reopened, CorrectiveActionStatus.Cancelled) => true,
            _ => false
        };

    public static void EnsureAllowed(CorrectiveActionStatus from, CorrectiveActionStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException(
                $"انتقال حالة الإجراء التصحيحي غير مسموح من {CorrectiveActionDisplay.StatusAr(from)} إلى {CorrectiveActionDisplay.StatusAr(to)}.");
        }
    }

    public static bool IsOverdue(CorrectiveActionStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow) =>
        dueAtUtc.HasValue &&
        dueAtUtc.Value < utcNow &&
        status is not CorrectiveActionStatus.Completed and not CorrectiveActionStatus.Cancelled;

    public static bool IsDueSoon(CorrectiveActionStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow, int days) =>
        dueAtUtc.HasValue &&
        dueAtUtc.Value >= utcNow &&
        dueAtUtc.Value <= utcNow.AddDays(Math.Max(days, 0)) &&
        status is not CorrectiveActionStatus.Completed and not CorrectiveActionStatus.Cancelled;
}

public static class CorrectiveActionReferenceFormatter
{
    public static string Format(long sequence) => $"CA-{sequence:00000000}";
}
