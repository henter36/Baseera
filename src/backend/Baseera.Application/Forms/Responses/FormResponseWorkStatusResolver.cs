namespace Baseera.Application.Forms.Responses;

using Baseera.Domain.Forms;

public static class FormResponseWorkStatusResolver
{
    public static DateTimeOffset ResolveEffectiveDueAt(
        DateTimeOffset cycleDueAtUtc,
        DateTimeOffset? dueAtOverride) =>
        dueAtOverride ?? cycleDueAtUtc;

    public static bool IsOverdue(
        FormResponseStatus? status,
        FormCompletionBasis basis,
        DateTimeOffset effectiveDueAtUtc,
        DateTimeOffset nowUtc,
        IFormResponseCompletionEvaluator completion)
    {
        if (completion.IsCompleted(basis, status))
        {
            return false;
        }

        return nowUtc > effectiveDueAtUtc;
    }

    public static FormAssignmentWorkStatus Resolve(
        FormResponseStatus? status,
        bool isOverdue)
    {
        if (status is null)
        {
            return isOverdue ? FormAssignmentWorkStatus.Overdue : FormAssignmentWorkStatus.NotStarted;
        }

        if (isOverdue && status is FormResponseStatus.Draft or FormResponseStatus.Returned)
        {
            return FormAssignmentWorkStatus.Overdue;
        }

        return status switch
        {
            FormResponseStatus.Draft => FormAssignmentWorkStatus.Draft,
            FormResponseStatus.Submitted => FormAssignmentWorkStatus.Submitted,
            FormResponseStatus.UnderReview => FormAssignmentWorkStatus.UnderReview,
            FormResponseStatus.Returned => FormAssignmentWorkStatus.Returned,
            FormResponseStatus.Approved => FormAssignmentWorkStatus.Approved,
            FormResponseStatus.Rejected => FormAssignmentWorkStatus.Rejected,
            FormResponseStatus.Closed => FormAssignmentWorkStatus.Closed,
            _ => FormAssignmentWorkStatus.NotStarted
        };
    }
}
