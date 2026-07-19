using Baseera.Application.Notes;
using Baseera.Domain.Notes;

namespace Baseera.UnitTests;

public sealed class NoteStateMachineTests
{
    [Theory]
    [InlineData(NoteStatus.Draft, NoteStatus.Open)]
    [InlineData(NoteStatus.Draft, NoteStatus.Cancelled)]
    [InlineData(NoteStatus.Open, NoteStatus.Assigned)]
    [InlineData(NoteStatus.Open, NoteStatus.Cancelled)]
    [InlineData(NoteStatus.Assigned, NoteStatus.InProgress)]
    [InlineData(NoteStatus.Assigned, NoteStatus.Assigned)]
    [InlineData(NoteStatus.InProgress, NoteStatus.PendingVerification)]
    [InlineData(NoteStatus.PendingVerification, NoteStatus.Closed)]
    [InlineData(NoteStatus.PendingVerification, NoteStatus.InProgress)]
    [InlineData(NoteStatus.Closed, NoteStatus.Reopened)]
    [InlineData(NoteStatus.Reopened, NoteStatus.Assigned)]
    [InlineData(NoteStatus.Reopened, NoteStatus.InProgress)]
    public void Allowed_transitions_are_accepted(NoteStatus from, NoteStatus to)
    {
        Assert.True(NoteStateMachine.CanTransition(from, to));
        NoteStateMachine.EnsureAllowed(from, to);
    }

    [Theory]
    [InlineData(NoteStatus.Draft, NoteStatus.Assigned)]
    [InlineData(NoteStatus.Draft, NoteStatus.InProgress)]
    [InlineData(NoteStatus.Draft, NoteStatus.Closed)]
    [InlineData(NoteStatus.Open, NoteStatus.InProgress)]
    [InlineData(NoteStatus.Open, NoteStatus.Open)]
    [InlineData(NoteStatus.Assigned, NoteStatus.Open)]
    [InlineData(NoteStatus.Assigned, NoteStatus.PendingVerification)]
    [InlineData(NoteStatus.InProgress, NoteStatus.Assigned)]
    [InlineData(NoteStatus.InProgress, NoteStatus.Closed)]
    [InlineData(NoteStatus.PendingVerification, NoteStatus.Cancelled)]
    [InlineData(NoteStatus.Closed, NoteStatus.InProgress)]
    [InlineData(NoteStatus.Closed, NoteStatus.Cancelled)]
    [InlineData(NoteStatus.Cancelled, NoteStatus.Open)]
    [InlineData(NoteStatus.Cancelled, NoteStatus.Draft)]
    [InlineData(NoteStatus.Reopened, NoteStatus.Closed)]
    [InlineData(NoteStatus.Reopened, NoteStatus.Cancelled)]
    public void Disallowed_transitions_are_rejected(NoteStatus from, NoteStatus to)
    {
        Assert.False(NoteStateMachine.CanTransition(from, to));
        Assert.Throws<InvalidOperationException>(() => NoteStateMachine.EnsureAllowed(from, to));
    }

    [Theory]
    [InlineData(NoteStatus.Closed)]
    [InlineData(NoteStatus.Cancelled)]
    public void Terminal_locked_statuses_are_flagged(NoteStatus status) =>
        Assert.True(NoteStateMachine.IsTerminalLocked(status));

    [Theory]
    [InlineData(NoteStatus.Draft)]
    [InlineData(NoteStatus.Open)]
    [InlineData(NoteStatus.Assigned)]
    [InlineData(NoteStatus.InProgress)]
    [InlineData(NoteStatus.PendingVerification)]
    [InlineData(NoteStatus.Reopened)]
    public void Non_terminal_statuses_are_not_flagged(NoteStatus status) =>
        Assert.False(NoteStateMachine.IsTerminalLocked(status));

    [Fact]
    public void Overdue_requires_past_due_date_and_non_terminal_status()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(NoteStateMachine.IsOverdue(NoteStatus.InProgress, now.AddDays(-1), now));
        Assert.False(NoteStateMachine.IsOverdue(NoteStatus.InProgress, now.AddDays(1), now));
        Assert.False(NoteStateMachine.IsOverdue(NoteStatus.InProgress, null, now));
    }

    [Theory]
    [InlineData(NoteStatus.Closed)]
    [InlineData(NoteStatus.Cancelled)]
    public void Overdue_is_false_once_note_reaches_a_terminal_status(NoteStatus status)
    {
        var now = DateTimeOffset.UtcNow;
        Assert.False(NoteStateMachine.IsOverdue(status, now.AddDays(-1), now));
    }

    [Theory]
    [InlineData(1, "OBS-00000001")]
    [InlineData(42, "OBS-00000042")]
    [InlineData(123456789, "OBS-123456789")]
    public void Reference_formatter_pads_to_eight_digits_and_keeps_obs_prefix(long sequence, string expected) =>
        Assert.Equal(expected, NoteReferenceFormatter.Format(sequence));
}
