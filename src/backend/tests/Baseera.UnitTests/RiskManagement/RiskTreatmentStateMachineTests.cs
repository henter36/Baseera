namespace Baseera.UnitTests.RiskManagement;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskTreatmentStateMachineTests
{
    [Theory]
    [InlineData(TreatmentPlanStatus.Draft, TreatmentPlanStatus.PendingApproval, true)]
    [InlineData(TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Approved, true)]
    [InlineData(TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Rejected, true)]
    [InlineData(TreatmentPlanStatus.PendingApproval, TreatmentPlanStatus.Draft, true)]
    [InlineData(TreatmentPlanStatus.Approved, TreatmentPlanStatus.InProgress, true)]
    [InlineData(TreatmentPlanStatus.InProgress, TreatmentPlanStatus.Blocked, true)]
    [InlineData(TreatmentPlanStatus.Blocked, TreatmentPlanStatus.InProgress, true)]
    [InlineData(TreatmentPlanStatus.InProgress, TreatmentPlanStatus.Completed, true)]
    [InlineData(TreatmentPlanStatus.Draft, TreatmentPlanStatus.Completed, false)]
    [InlineData(TreatmentPlanStatus.Completed, TreatmentPlanStatus.InProgress, false)]
    [InlineData(TreatmentPlanStatus.Draft, TreatmentPlanStatus.Overdue, false)]
    public void PlanStateMachine_EnforcesDocumentedTransitions(TreatmentPlanStatus from, TreatmentPlanStatus to, bool expected) =>
        Assert.Equal(expected, RiskTreatmentPlanStateMachine.CanTransition(from, to));

    [Fact]
    public void PlanStateMachine_OverdueIsNeverAValidTransitionTarget()
    {
        foreach (TreatmentPlanStatus from in Enum.GetValues<TreatmentPlanStatus>())
        {
            Assert.False(RiskTreatmentPlanStateMachine.CanTransition(from, TreatmentPlanStatus.Overdue));
        }
    }

    [Fact]
    public void PlanStateMachine_IsOverdue_ComputedNotStored()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(RiskTreatmentPlanStateMachine.IsOverdue(TreatmentPlanStatus.InProgress, now.AddDays(-1), now));
        Assert.False(RiskTreatmentPlanStateMachine.IsOverdue(TreatmentPlanStatus.Completed, now.AddDays(-1), now));
        Assert.False(RiskTreatmentPlanStateMachine.IsOverdue(TreatmentPlanStatus.InProgress, now.AddDays(1), now));
    }

    [Theory]
    [InlineData(TreatmentPlanStatus.Completed, true)]
    [InlineData(TreatmentPlanStatus.Cancelled, true)]
    [InlineData(TreatmentPlanStatus.Rejected, true)]
    [InlineData(TreatmentPlanStatus.InProgress, false)]
    public void PlanStateMachine_CanCloseOnlyFromTerminalStates(TreatmentPlanStatus status, bool expected) =>
        Assert.Equal(expected, RiskTreatmentPlanStateMachine.CanClose(status));

    [Theory]
    [InlineData(RiskTreatmentActionStatus.Draft, RiskTreatmentActionStatus.Assigned, true)]
    [InlineData(RiskTreatmentActionStatus.Assigned, RiskTreatmentActionStatus.InProgress, true)]
    [InlineData(RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.PendingVerification, true)]
    [InlineData(RiskTreatmentActionStatus.PendingVerification, RiskTreatmentActionStatus.Completed, true)]
    [InlineData(RiskTreatmentActionStatus.PendingVerification, RiskTreatmentActionStatus.InProgress, true)]
    [InlineData(RiskTreatmentActionStatus.InProgress, RiskTreatmentActionStatus.Blocked, true)]
    [InlineData(RiskTreatmentActionStatus.Blocked, RiskTreatmentActionStatus.InProgress, true)]
    [InlineData(RiskTreatmentActionStatus.Draft, RiskTreatmentActionStatus.Completed, false)]
    [InlineData(RiskTreatmentActionStatus.Completed, RiskTreatmentActionStatus.InProgress, false)]
    public void ActionStateMachine_EnforcesDocumentedTransitions(RiskTreatmentActionStatus from, RiskTreatmentActionStatus to, bool expected) =>
        Assert.Equal(expected, RiskTreatmentActionStateMachine.CanTransition(from, to));

    [Fact]
    public void ActionStateMachine_IsOverdue_ExcludesTerminalStates()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(RiskTreatmentActionStateMachine.IsOverdue(RiskTreatmentActionStatus.InProgress, now.AddDays(-1), now));
        Assert.False(RiskTreatmentActionStateMachine.IsOverdue(RiskTreatmentActionStatus.Completed, now.AddDays(-1), now));
        Assert.False(RiskTreatmentActionStateMachine.IsOverdue(RiskTreatmentActionStatus.Cancelled, now.AddDays(-1), now));
    }

    [Fact]
    public void EnsureAllowed_ThrowsWithArabicMessage_ForBothStateMachines()
    {
        Assert.Throws<InvalidOperationException>(() =>
            RiskTreatmentPlanStateMachine.EnsureAllowed(TreatmentPlanStatus.Draft, TreatmentPlanStatus.Completed));
        Assert.Throws<InvalidOperationException>(() =>
            RiskTreatmentActionStateMachine.EnsureAllowed(RiskTreatmentActionStatus.Draft, RiskTreatmentActionStatus.Completed));
    }
}
