namespace Baseera.UnitTests.RiskManagement;

using Baseera.Application.RiskManagement;
using Baseera.Domain.RiskManagement;

public sealed class RiskLifecycleStateMachineTests
{
    [Theory]
    [InlineData(RiskStatus.Draft, RiskStatus.UnderAssessment, true)]
    [InlineData(RiskStatus.UnderAssessment, RiskStatus.PendingReview, true)]
    [InlineData(RiskStatus.PendingReview, RiskStatus.Active, true)]
    [InlineData(RiskStatus.PendingReview, RiskStatus.UnderAssessment, true)]
    [InlineData(RiskStatus.Active, RiskStatus.UnderTreatment, true)]
    [InlineData(RiskStatus.Active, RiskStatus.Monitoring, true)]
    [InlineData(RiskStatus.Active, RiskStatus.PendingAcceptance, true)]
    [InlineData(RiskStatus.UnderTreatment, RiskStatus.Monitoring, true)]
    [InlineData(RiskStatus.PendingAcceptance, RiskStatus.Accepted, true)]
    [InlineData(RiskStatus.PendingAcceptance, RiskStatus.Active, true)]
    [InlineData(RiskStatus.Accepted, RiskStatus.PendingReview, true)]
    [InlineData(RiskStatus.Monitoring, RiskStatus.PendingClosure, true)]
    [InlineData(RiskStatus.Monitoring, RiskStatus.UnderTreatment, true)]
    [InlineData(RiskStatus.PendingClosure, RiskStatus.Closed, true)]
    [InlineData(RiskStatus.PendingClosure, RiskStatus.Monitoring, true)]
    [InlineData(RiskStatus.Closed, RiskStatus.Reopened, true)]
    [InlineData(RiskStatus.Reopened, RiskStatus.UnderAssessment, true)]
    [InlineData(RiskStatus.Closed, RiskStatus.Archived, true)]
    public void CanTransition_AllowsDocumentedEdges(RiskStatus from, RiskStatus to, bool expected) =>
        Assert.Equal(expected, RiskLifecycleStateMachine.CanTransition(from, to));

    [Theory]
    [InlineData(RiskStatus.Draft, RiskStatus.Active)]
    [InlineData(RiskStatus.Draft, RiskStatus.Closed)]
    [InlineData(RiskStatus.Active, RiskStatus.Closed)]
    [InlineData(RiskStatus.Active, RiskStatus.Archived)]
    [InlineData(RiskStatus.Accepted, RiskStatus.Closed)]
    [InlineData(RiskStatus.UnderTreatment, RiskStatus.Archived)]
    [InlineData(RiskStatus.Monitoring, RiskStatus.Archived)]
    [InlineData(RiskStatus.Reopened, RiskStatus.Active)]
    [InlineData(RiskStatus.Closed, RiskStatus.Draft)]
    public void CanTransition_RejectsUndocumentedEdges(RiskStatus from, RiskStatus to) =>
        Assert.False(RiskLifecycleStateMachine.CanTransition(from, to));

    [Fact]
    public void EnsureAllowed_ThrowsOnInvalidTransition()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.Draft, RiskStatus.Closed));
        Assert.Contains("انتقال حالة الخطر غير مسموح", ex.Message);
    }

    [Fact]
    public void EnsureAllowed_DoesNotThrowOnValidTransition() =>
        RiskLifecycleStateMachine.EnsureAllowed(RiskStatus.Draft, RiskStatus.UnderAssessment);

    [Fact]
    public void IsDeletable_OnlyTrueForDraft()
    {
        Assert.True(RiskLifecycleStateMachine.IsDeletable(RiskStatus.Draft));
        Assert.False(RiskLifecycleStateMachine.IsDeletable(RiskStatus.Active));
        Assert.False(RiskLifecycleStateMachine.IsDeletable(RiskStatus.Closed));
    }

    [Fact]
    public void IsAcceptanceExpired_TrueOnlyWhenAcceptedAndPastWindow()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(RiskLifecycleStateMachine.IsAcceptanceExpired(RiskStatus.Accepted, now.AddDays(-1), now));
        Assert.False(RiskLifecycleStateMachine.IsAcceptanceExpired(RiskStatus.Accepted, now.AddDays(1), now));
        Assert.False(RiskLifecycleStateMachine.IsAcceptanceExpired(RiskStatus.Active, now.AddDays(-1), now));
        Assert.False(RiskLifecycleStateMachine.IsAcceptanceExpired(RiskStatus.Accepted, null, now));
    }

    [Fact]
    public void IsReviewOverdue_TrueOnlyWhenDueDateHasPassed()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.True(RiskLifecycleStateMachine.IsReviewOverdue(now.AddDays(-1), now));
        Assert.False(RiskLifecycleStateMachine.IsReviewOverdue(now.AddDays(1), now));
        Assert.False(RiskLifecycleStateMachine.IsReviewOverdue(null, now));
    }
}
