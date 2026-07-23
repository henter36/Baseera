namespace Baseera.Application.Forms.Responses;

using Baseera.Domain.Forms;

public interface IFormResponseCompletionEvaluator
{
    bool IsCompleted(FormCompletionBasis basis, FormResponseStatus? status);
}

public sealed class FormResponseCompletionEvaluator : IFormResponseCompletionEvaluator
{
    public bool IsCompleted(FormCompletionBasis basis, FormResponseStatus? status)
    {
        if (status is null)
        {
            return false;
        }

        return basis switch
        {
            FormCompletionBasis.Submitted => status is FormResponseStatus.Submitted
                or FormResponseStatus.UnderReview
                or FormResponseStatus.Approved
                or FormResponseStatus.Closed,
            FormCompletionBasis.Approved => status is FormResponseStatus.Approved
                or FormResponseStatus.Closed,
            _ => false
        };
    }
}
