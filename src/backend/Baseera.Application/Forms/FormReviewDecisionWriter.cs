namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;

internal static class FormReviewDecisionWriter
{
    public static void Append(
        IBaseeraDbContext db,
        Guid formId,
        FormReviewDecisionType decision,
        FormDefinitionStatus from,
        FormDefinitionStatus to,
        Guid userId,
        string reason,
        bool isAdministrativeOverride)
    {
        db.Add(new FormReviewDecision
        {
            FormDefinitionId = formId,
            Decision = decision,
            Reason = reason,
            ReviewedByUserId = userId,
            ReviewedAtUtc = DateTimeOffset.UtcNow,
            FromStatus = from,
            ToStatus = to,
            IsAdministrativeOverride = isAdministrativeOverride
        });
    }
}
