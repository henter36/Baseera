namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormSeparationOfDutiesService
{
    Task EnforceSubmitForReviewAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default);

    Task EnforceReviewAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default);

    Task EnforceApproveAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default);

    Task EnforceGrantAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default);
}

public sealed class FormSeparationOfDutiesService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IAuditService audit) : IFormSeparationOfDutiesService
{
    public Task EnforceSubmitForReviewAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default) =>
        EnforceAsync(
            form,
            actorId,
            administrativeOverrideReason,
            "FormSubmitForReview",
            async (policy, ct) =>
            {
                if (!policy.RequireSeparationOfDuties)
                {
                    return;
                }

                // Submit is allowed for creator by default; no additional SoD unless extended later.
                await Task.CompletedTask;
            },
            cancellationToken);

    public Task EnforceReviewAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default) =>
        EnforceAsync(
            form,
            actorId,
            administrativeOverrideReason,
            "FormReview",
            async (policy, ct) =>
            {
                if (!policy.RequireSeparationOfDuties)
                {
                    return;
                }

                if (form.CreatedByUserId == actorId && !policy.AllowDesignerToReviewOwnForm)
                {
                    throw new InvalidOperationException("فصل الواجبات: لا يمكن لمنشئ النموذج مراجعته.");
                }

                await Task.CompletedTask;
            },
            cancellationToken);

    public Task EnforceApproveAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default) =>
        EnforceAsync(
            form,
            actorId,
            administrativeOverrideReason,
            "FormApprove",
            async (policy, ct) =>
            {
                if (!policy.RequireSeparationOfDuties)
                {
                    return;
                }

                if (form.LastModifiedByUserId == actorId)
                {
                    throw new InvalidOperationException("فصل الواجبات: لا يمكن لآخر من عدّل النموذج اعتماده.");
                }

                if (!policy.AllowReviewerToApproveOwnReview)
                {
                    var reviewed = await db.FormReviewDecisions.AnyAsync(
                        d =>
                            d.FormDefinitionId == form.Id &&
                            d.ReviewedByUserId == actorId &&
                            d.Decision == FormReviewDecisionType.RequestChanges,
                        ct);
                    if (reviewed)
                    {
                        throw new InvalidOperationException("فصل الواجبات: لا يمكن للمراجع اعتماد النموذج بعد طلب التعديلات.");
                    }
                }

                await Task.CompletedTask;
            },
            cancellationToken);

    public Task EnforceGrantAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason = null,
        CancellationToken cancellationToken = default) =>
        EnforceAsync(
            form,
            actorId,
            administrativeOverrideReason,
            "FormGrant",
            async (policy, ct) =>
            {
                if (!policy.RequireSeparationOfDuties)
                {
                    return;
                }

                if (form.CreatedByUserId == actorId)
                {
                    throw new InvalidOperationException("فصل الواجبات: لا يمكن لمنشئ النموذج منح صلاحيات وصول عليه.");
                }

                await Task.CompletedTask;
            },
            cancellationToken);

    private async Task EnforceAsync(
        FormDefinition form,
        Guid actorId,
        string? administrativeOverrideReason,
        string action,
        Func<FormGovernancePolicy, CancellationToken, Task> check,
        CancellationToken cancellationToken)
    {
        var policy = await GetPolicyAsync(cancellationToken);
        try
        {
            await check(policy, cancellationToken);
        }
        catch (InvalidOperationException) when (CanAdministrativeOverride(administrativeOverrideReason))
        {
            await audit.WriteAsync(new AuditEntry
            {
                Action = "FormAdministrativeOverride",
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormDefinition),
                EntityId = form.Id.ToString(),
                NewValues = new { action, actorId },
                Reason = administrativeOverrideReason!.Trim(),
                Outcome = "Override"
            }, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private bool CanAdministrativeOverride(string? reason) =>
        !string.IsNullOrWhiteSpace(reason) &&
        currentUser.HasPermission(PermissionCodes.FormsManageGovernance);

    private async Task<FormGovernancePolicy> GetPolicyAsync(CancellationToken cancellationToken) =>
        await db.FormGovernancePolicies.AsNoTracking().OrderBy(p => p.CreatedAtUtc).FirstAsync(cancellationToken);
}
