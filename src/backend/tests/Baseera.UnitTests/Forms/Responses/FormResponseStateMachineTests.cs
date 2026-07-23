using Baseera.Application.Forms.Responses;
using Baseera.Application.Forms.Compliance;
using Baseera.Domain.Forms;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormResponseStateMachineTests
{
    [Theory]
    [InlineData(null, FormResponseStatus.Draft, true)]
    [InlineData(FormResponseStatus.Draft, FormResponseStatus.Submitted, true)]
    [InlineData(FormResponseStatus.Returned, FormResponseStatus.Draft, true)]
    [InlineData(FormResponseStatus.Submitted, FormResponseStatus.UnderReview, true)]
    [InlineData(FormResponseStatus.Submitted, FormResponseStatus.Approved, true)]
    [InlineData(FormResponseStatus.Submitted, FormResponseStatus.Closed, true)]
    [InlineData(FormResponseStatus.UnderReview, FormResponseStatus.Returned, true)]
    [InlineData(FormResponseStatus.UnderReview, FormResponseStatus.Approved, true)]
    [InlineData(FormResponseStatus.UnderReview, FormResponseStatus.Rejected, true)]
    [InlineData(FormResponseStatus.Approved, FormResponseStatus.Closed, true)]
    [InlineData(FormResponseStatus.Rejected, FormResponseStatus.Draft, false)]
    [InlineData(FormResponseStatus.Closed, FormResponseStatus.Draft, false)]
    [InlineData(FormResponseStatus.Submitted, FormResponseStatus.Draft, false)]
    public void CanTransition_matches_matrix(FormResponseStatus? from, FormResponseStatus to, bool expected)
    {
        Assert.Equal(expected, FormResponseStateMachine.CanTransition(from, to));
    }

    [Fact]
    public void ResolveSubmissionTargetStatus_none_stays_submitted()
    {
        Assert.Equal(FormResponseStatus.Submitted,
            FormResponseStateMachine.ResolveSubmissionTargetStatus(FormReviewMode.None));
        Assert.Equal(FormResponseStatus.UnderReview,
            FormResponseStateMachine.ResolveSubmissionTargetStatus(FormReviewMode.SingleLevel));
    }

    [Fact]
    public void ResolveApprovalTargetStatus_final_and_intermediate()
    {
        Assert.Equal(FormResponseStatus.UnderReview,
            FormResponseStateMachine.ResolveApprovalTargetStatus(1, 3));
        Assert.Equal(FormResponseStatus.Approved,
            FormResponseStateMachine.ResolveApprovalTargetStatus(3, 3));
    }

    [Fact]
    public void EnsureCanTransition_throws_for_illegal()
    {
        Assert.Throws<InvalidOperationException>(() =>
            FormResponseStateMachine.EnsureCanTransition(FormResponseStatus.Closed, FormResponseStatus.Draft));
    }
}

public sealed class FormResponseCompletionEvaluatorTests
{
    private readonly FormResponseCompletionEvaluator _sut = new();

    [Theory]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Submitted, true)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.UnderReview, true)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Approved, true)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Closed, true)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Draft, false)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Returned, false)]
    [InlineData(FormCompletionBasis.Submitted, FormResponseStatus.Rejected, false)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Approved, true)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Closed, true)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Draft, false)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Submitted, false)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.UnderReview, false)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Returned, false)]
    [InlineData(FormCompletionBasis.Approved, FormResponseStatus.Rejected, false)]
    public void IsCompleted_by_basis(FormCompletionBasis basis, FormResponseStatus status, bool expected)
    {
        Assert.Equal(expected, _sut.IsCompleted(basis, status));
        Assert.False(_sut.IsCompleted(basis, null));
    }
}

public sealed class FormResponseWorkStatusResolverTests
{
    private readonly FormResponseCompletionEvaluator _completion = new();

    [Fact]
    public void NotStarted_when_no_response_and_not_overdue()
    {
        var due = DateTimeOffset.UtcNow.AddDays(1);
        var overdue = FormResponseWorkStatusResolver.IsOverdue(null, FormCompletionBasis.Submitted, due, DateTimeOffset.UtcNow, _completion);
        Assert.False(overdue);
        Assert.Equal(FormAssignmentWorkStatus.NotStarted, FormResponseWorkStatusResolver.Resolve(null, overdue));
    }

    [Fact]
    public void Overdue_preserves_actual_status_flag_separately()
    {
        var due = DateTimeOffset.UtcNow.AddDays(-1);
        var overdue = FormResponseWorkStatusResolver.IsOverdue(FormResponseStatus.Draft, FormCompletionBasis.Submitted, due, DateTimeOffset.UtcNow, _completion);
        Assert.True(overdue);
        Assert.Equal(FormAssignmentWorkStatus.Overdue, FormResponseWorkStatusResolver.Resolve(FormResponseStatus.Draft, overdue));
        Assert.Equal(due, FormResponseWorkStatusResolver.ResolveEffectiveDueAt(due, null));
        var overrideDue = due.AddDays(2);
        Assert.Equal(overrideDue, FormResponseWorkStatusResolver.ResolveEffectiveDueAt(due, overrideDue));
    }

    [Fact]
    public void Completed_is_never_overdue()
    {
        var due = DateTimeOffset.UtcNow.AddDays(-10);
        Assert.False(FormResponseWorkStatusResolver.IsOverdue(
            FormResponseStatus.Approved, FormCompletionBasis.Approved, due, DateTimeOffset.UtcNow, _completion));
    }
}

public sealed class FormCompletionTimestampResolverTests
{
    private readonly FormCompletionTimestampResolver _sut = new();

    [Fact]
    public void Submitted_basis_uses_submitted_at_only()
    {
        var submittedAt = DateTimeOffset.UtcNow.AddHours(-2);
        var response = new FormResponse
        {
            SubmittedAtUtc = submittedAt,
            ApprovedAtUtc = submittedAt.AddHours(1),
            ClosedAtUtc = submittedAt.AddHours(2)
        };

        Assert.Equal(submittedAt, _sut.Resolve(FormCompletionBasis.Submitted, response));
    }

    [Fact]
    public void Approved_basis_uses_approved_at_only()
    {
        var approvedAt = DateTimeOffset.UtcNow.AddHours(-1);
        var response = new FormResponse
        {
            SubmittedAtUtc = approvedAt.AddHours(-1),
            ApprovedAtUtc = approvedAt,
            ClosedAtUtc = approvedAt.AddHours(1)
        };

        Assert.Equal(approvedAt, _sut.Resolve(FormCompletionBasis.Approved, response));
    }

    [Fact]
    public void Missing_timestamp_is_not_replaced_by_closed_at()
    {
        var response = new FormResponse
        {
            Status = FormResponseStatus.Approved,
            ClosedAtUtc = DateTimeOffset.UtcNow
        };

        Assert.Null(_sut.Resolve(FormCompletionBasis.Approved, response));
    }

    [Fact]
    public void Missing_response_returns_null()
    {
        Assert.Null(_sut.Resolve(FormCompletionBasis.Submitted, null));
    }
}

public sealed class FormResponsePolicyRulesTests
{
    [Fact]
    public void Validates_review_mode_levels_and_completion()
    {
        Assert.Throws<ArgumentException>(() => FormResponsePolicyRules.Validate(
            new FormCampaignResponsePolicyRequest(FormCompletionBasis.Submitted, FormReviewMode.None, 1, true, true, false, true)));
        Assert.Throws<ArgumentException>(() => FormResponsePolicyRules.Validate(
            new FormCampaignResponsePolicyRequest(FormCompletionBasis.Submitted, FormReviewMode.SingleLevel, 2, true, true, false, true)));
        Assert.Throws<ArgumentException>(() => FormResponsePolicyRules.Validate(
            new FormCampaignResponsePolicyRequest(FormCompletionBasis.Submitted, FormReviewMode.MultiLevel, 1, true, true, false, true)));
        Assert.Throws<ArgumentException>(() => FormResponsePolicyRules.Validate(
            new FormCampaignResponsePolicyRequest(FormCompletionBasis.Approved, FormReviewMode.None, 0, true, true, false, true)));
        FormResponsePolicyRules.Validate(
            new FormCampaignResponsePolicyRequest(FormCompletionBasis.Approved, FormReviewMode.MultiLevel, 3, true, true, false, true));
    }
}
