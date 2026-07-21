namespace Baseera.Application.Dashboard;

using Baseera.Application.CorrectiveActions;
using Baseera.Application.Notes;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Notes;

internal static class OperationalDashboardKpiDefinitions
{
    public const int TopLimit = 10;
    public const int MaxTrendDays = 90;
    public const int DefaultDueSoonDays = 7;
    public static readonly int[] AllowedPeriodDays = [7, 30, 90];

    public static readonly NoteStatus[] TerminalNoteStatuses =
    [
        NoteStatus.Closed,
        NoteStatus.Cancelled
    ];

    public static readonly CorrectiveActionStatus[] TerminalCorrectiveActionStatuses =
    [
        CorrectiveActionStatus.Completed,
        CorrectiveActionStatus.Cancelled
    ];

    public static bool IsOpenNote(NoteStatus status) =>
        status is not NoteStatus.Closed and not NoteStatus.Cancelled;

    public static bool IsOverdueNote(NoteStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow) =>
        NoteStateMachine.IsOverdue(status, dueAtUtc, utcNow);

    public static bool IsDueSoonNote(NoteStatus status, DateTimeOffset? dueAtUtc, DateTimeOffset utcNow, int days) =>
        dueAtUtc.HasValue &&
        dueAtUtc.Value >= utcNow &&
        dueAtUtc.Value <= utcNow.AddDays(Math.Max(days, 0)) &&
        IsOpenNote(status);

    public static bool IsCriticalOrHigh(NoteSeverity severity) =>
        severity is NoteSeverity.High or NoteSeverity.Critical;

    public static bool IsActiveCorrectiveAction(CorrectiveActionStatus status) =>
        status is not CorrectiveActionStatus.Completed and not CorrectiveActionStatus.Cancelled;

    public static bool IsOverdueCorrectiveAction(
        CorrectiveActionStatus status,
        DateTimeOffset? dueAtUtc,
        DateTimeOffset utcNow) =>
        CorrectiveActionStateMachine.IsOverdue(status, dueAtUtc, utcNow);
}
