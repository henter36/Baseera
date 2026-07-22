using Baseera.Application.Forms;
using Baseera.Domain.Forms;

namespace Baseera.UnitTests.Forms.Versions;

public sealed class FormVersionStateMachineTests
{
    [Theory]
    [InlineData(FormVersionStatus.Draft, FormVersionStatus.InReview, true)]
    [InlineData(FormVersionStatus.InReview, FormVersionStatus.Locked, true)]
    [InlineData(FormVersionStatus.InReview, FormVersionStatus.ChangesRequested, true)]
    [InlineData(FormVersionStatus.InReview, FormVersionStatus.Rejected, true)]
    [InlineData(FormVersionStatus.ChangesRequested, FormVersionStatus.InReview, true)]
    [InlineData(FormVersionStatus.Rejected, FormVersionStatus.Draft, true)]
    [InlineData(FormVersionStatus.Locked, FormVersionStatus.Draft, false)]
    [InlineData(FormVersionStatus.Draft, FormVersionStatus.Locked, false)]
    public void Transition_matrix(FormVersionStatus from, FormVersionStatus to, bool allowed)
    {
        Assert.Equal(allowed, FormVersionStateMachine.CanTransition(from, to));
        if (!allowed)
        {
            Assert.Throws<InvalidOperationException>(() => FormVersionStateMachine.EnsureAllowed(from, to));
        }
        else
        {
            FormVersionStateMachine.EnsureAllowed(from, to);
        }
    }

    [Fact]
    public void Locked_is_not_editable()
    {
        Assert.False(FormVersionStateMachine.IsEditable(FormVersionStatus.Locked));
        Assert.Throws<InvalidOperationException>(() => FormVersionStateMachine.EnsureEditable(FormVersionStatus.Locked));
    }
}
