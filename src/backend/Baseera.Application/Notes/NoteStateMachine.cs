namespace Baseera.Application.Notes;

using Baseera.Domain.Notes;

/// <summary>
/// Pure transition rules for operational notes (no I/O).
/// </summary>
public static class NoteStateMachine
{
    public static bool CanTransition(NoteStatus from, NoteStatus to) =>
        (from, to) switch
        {
            (NoteStatus.Draft, NoteStatus.Open) => true,
            (NoteStatus.Draft, NoteStatus.Cancelled) => true,
            (NoteStatus.Open, NoteStatus.Assigned) => true,
            (NoteStatus.Open, NoteStatus.Cancelled) => true,
            (NoteStatus.Assigned, NoteStatus.InProgress) => true,
            (NoteStatus.Assigned, NoteStatus.Assigned) => true,
            (NoteStatus.InProgress, NoteStatus.PendingVerification) => true,
            (NoteStatus.PendingVerification, NoteStatus.Closed) => true,
            (NoteStatus.PendingVerification, NoteStatus.InProgress) => true,
            (NoteStatus.Closed, NoteStatus.Reopened) => true,
            (NoteStatus.Reopened, NoteStatus.Assigned) => true,
            (NoteStatus.Reopened, NoteStatus.InProgress) => true,
            // Phase 1B decision-approval closures (Invalid/Duplicate/NoAction) close a note without ever
            // routing through PendingVerification/VerifyClosure — they carry their own four-eyes approval
            // (NoteDecisionApprovalService) and are tagged with a distinct ClosureReason. Still one single
            // state machine: these are additional rows in the same authoritative table, not a parallel one.
            (NoteStatus.Open, NoteStatus.Closed) => true,
            (NoteStatus.Assigned, NoteStatus.Closed) => true,
            (NoteStatus.InProgress, NoteStatus.Closed) => true,
            _ => false
        };

    public static void EnsureAllowed(NoteStatus from, NoteStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidOperationException($"انتقال الحالة من {NoteDisplay.StatusAr(from)} إلى {NoteDisplay.StatusAr(to)} غير مسموح.");
        }
    }

    public static bool IsTerminalLocked(NoteStatus status) =>
        status is NoteStatus.Cancelled or NoteStatus.Closed;

    public static bool IsOverdue(NoteStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow) =>
        dueAtUtc.HasValue
        && dueAtUtc.Value < utcNow
        && status is not NoteStatus.Closed and not NoteStatus.Cancelled;
}

public static class NoteReferenceFormatter
{
    public static string Format(long sequenceValue) => $"OBS-{sequenceValue:D8}";
}
