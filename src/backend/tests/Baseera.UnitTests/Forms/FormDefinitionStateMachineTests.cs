using Baseera.Application.Forms;
using Baseera.Domain.Forms;

namespace Baseera.UnitTests.Forms;

public sealed class FormDefinitionStateMachineTests
{
    [Theory]
    [InlineData(FormDefinitionStatus.Draft, FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.InReview, FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.InReview, FormDefinitionStatus.ChangesRequested)]
    [InlineData(FormDefinitionStatus.InReview, FormDefinitionStatus.Rejected)]
    [InlineData(FormDefinitionStatus.ChangesRequested, FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.Approved, FormDefinitionStatus.Archived)]
    [InlineData(FormDefinitionStatus.Rejected, FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.Rejected, FormDefinitionStatus.Archived)]
    [InlineData(FormDefinitionStatus.Archived, FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Archived, FormDefinitionStatus.Rejected)]
    public void Allowed_transitions_are_accepted(FormDefinitionStatus from, FormDefinitionStatus to)
    {
        Assert.True(FormDefinitionStateMachine.CanTransition(from, to));
        FormDefinitionStateMachine.EnsureAllowed(from, to);
    }

    [Theory]
    [InlineData(FormDefinitionStatus.Draft, FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Draft, FormDefinitionStatus.Archived)]
    [InlineData(FormDefinitionStatus.Draft, FormDefinitionStatus.Rejected)]
    [InlineData(FormDefinitionStatus.Draft, FormDefinitionStatus.ChangesRequested)]
    [InlineData(FormDefinitionStatus.InReview, FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.InReview, FormDefinitionStatus.Archived)]
    [InlineData(FormDefinitionStatus.ChangesRequested, FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.ChangesRequested, FormDefinitionStatus.Rejected)]
    [InlineData(FormDefinitionStatus.Approved, FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.Approved, FormDefinitionStatus.Rejected)]
    [InlineData(FormDefinitionStatus.Rejected, FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Rejected, FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.Archived, FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.Archived, FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.Archived, FormDefinitionStatus.ChangesRequested)]
    public void Disallowed_transitions_are_rejected(FormDefinitionStatus from, FormDefinitionStatus to)
    {
        Assert.False(FormDefinitionStateMachine.CanTransition(from, to));
        Assert.Throws<InvalidOperationException>(() => FormDefinitionStateMachine.EnsureAllowed(from, to));
    }

    [Theory]
    [InlineData(FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.ChangesRequested)]
    public void Editable_statuses_allow_updates(FormDefinitionStatus status) =>
        Assert.True(FormDefinitionStateMachine.IsEditable(status));

    [Theory]
    [InlineData(FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Rejected)]
    [InlineData(FormDefinitionStatus.Archived)]
    public void Non_editable_statuses_block_updates(FormDefinitionStatus status) =>
        Assert.False(FormDefinitionStateMachine.IsEditable(status));

    [Theory]
    [InlineData(FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Archived)]
    public void Terminal_locked_statuses_are_flagged(FormDefinitionStatus status) =>
        Assert.True(FormDefinitionStateMachine.IsTerminalLocked(status));

    [Theory]
    [InlineData(FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.ChangesRequested)]
    [InlineData(FormDefinitionStatus.Rejected)]
    public void Non_terminal_locked_statuses_are_not_flagged(FormDefinitionStatus status) =>
        Assert.False(FormDefinitionStateMachine.IsTerminalLocked(status));

    [Theory]
    [InlineData(FormDefinitionStatus.Approved)]
    [InlineData(FormDefinitionStatus.Rejected)]
    public void Restorable_prior_statuses_are_accepted(FormDefinitionStatus priorStatus)
    {
        Assert.True(FormDefinitionStateMachine.CanRestore(priorStatus));
        FormDefinitionStateMachine.EnsureRestorable(FormDefinitionStatus.Archived, priorStatus);
    }

    [Theory]
    [InlineData(FormDefinitionStatus.Draft)]
    [InlineData(FormDefinitionStatus.InReview)]
    [InlineData(FormDefinitionStatus.ChangesRequested)]
    [InlineData(FormDefinitionStatus.Archived)]
    public void Non_restorable_prior_statuses_are_rejected(FormDefinitionStatus priorStatus)
    {
        Assert.False(FormDefinitionStateMachine.CanRestore(priorStatus));
        Assert.Throws<InvalidOperationException>(() =>
            FormDefinitionStateMachine.EnsureRestorable(FormDefinitionStatus.Archived, priorStatus));
    }

    [Fact]
    public void EnsureRestorable_requires_from_archived() =>
        Assert.Throws<InvalidOperationException>(() =>
            FormDefinitionStateMachine.EnsureRestorable(FormDefinitionStatus.Approved, FormDefinitionStatus.Approved));
}
