namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;

internal sealed record FormReviewDecisionWriteRequest(
    Guid FormId,
    FormReviewDecisionType Decision,
    FormDefinitionStatus From,
    FormDefinitionStatus To,
    Guid UserId,
    string? Reason,
    bool IsAdministrativeOverride);

internal static class FormReviewDecisionWriter
{
    public static void Append(IBaseeraDbContext db, FormReviewDecisionWriteRequest request)
    {
        db.Add(new FormReviewDecision
        {
            FormDefinitionId = request.FormId,
            Decision = request.Decision,
            Reason = request.Reason,
            ReviewedByUserId = request.UserId,
            ReviewedAtUtc = DateTimeOffset.UtcNow,
            FromStatus = request.From,
            ToStatus = request.To,
            IsAdministrativeOverride = request.IsAdministrativeOverride
        });
    }
}
