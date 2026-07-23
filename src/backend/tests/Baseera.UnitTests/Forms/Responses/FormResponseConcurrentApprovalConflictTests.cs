using System.Reflection;
using Baseera.Application.Forms.Responses;

namespace Baseera.UnitTests.Forms.Responses;

public sealed class FormResponseConcurrentApprovalConflictTests
{
    private static readonly MethodInfo DetectMethod = typeof(FormResponseReviewService)
        .GetMethod("IsConcurrentApprovalConflict", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("IsConcurrentApprovalConflict not found.");

    private static bool Detect(Exception exception) =>
        (bool)DetectMethod.Invoke(null, [exception])!;

    [Fact]
    public void Detects_approve_level_unique_index_in_exception_chain()
    {
        var inner = new InvalidOperationException(
            "Cannot insert duplicate key row. The duplicate key value violates unique index IX_FormResponseReviewDecisions_ApproveLevel.");
        var outer = new DbUpdateExceptionStub("Save failed", inner);

        Assert.True(Detect(outer));
    }

    [Fact]
    public void Unrelated_database_exceptions_are_not_treated_as_approval_conflict()
    {
        var inner = new InvalidOperationException("FK_FormResponses_Campaigns constraint failed.");
        var outer = new DbUpdateExceptionStub("Save failed", inner);

        Assert.False(Detect(outer));
    }

    private sealed class DbUpdateExceptionStub(string message, Exception inner) : Exception(message, inner);
}
