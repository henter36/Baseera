namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface IFormWorkflowService
{
    Task<FormDetailDto> SubmitForReviewAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormDetailDto> RequestChangesAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormDetailDto> ApproveAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
    Task<FormDetailDto> RejectAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default);
}

public sealed class FormWorkflowService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IFormScopeService formScope,
    IFormSeparationOfDutiesService sod,
    IAuditService audit,
    IFormQueryService queries) : IFormWorkflowService
{
    public Task<FormDetailDto> SubmitForReviewAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            PermissionCodes.FormsSubmitForReview,
            FormDefinitionStatus.InReview,
            FormReviewDecisionType.SubmitForReview,
            "FormSubmittedForReview",
            sod.EnforceSubmitForReviewAsync,
            ApplySubmitForReview,
            cancellationToken);

    public Task<FormDetailDto> RequestChangesAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            PermissionCodes.FormsRequestChanges,
            FormDefinitionStatus.ChangesRequested,
            FormReviewDecisionType.RequestChanges,
            "FormChangesRequested",
            sod.EnforceReviewAsync,
            null,
            cancellationToken);

    public Task<FormDetailDto> ApproveAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            PermissionCodes.FormsApprove,
            FormDefinitionStatus.Approved,
            FormReviewDecisionType.Approve,
            "FormApproved",
            sod.EnforceApproveAsync,
            ApplyApprove,
            cancellationToken);

    public Task<FormDetailDto> RejectAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            PermissionCodes.FormsReject,
            FormDefinitionStatus.Rejected,
            FormReviewDecisionType.Reject,
            "FormRejected",
            sod.EnforceReviewAsync,
            null,
            cancellationToken);

    private async Task<FormDetailDto> TransitionAsync(
        Guid id,
        FormTransitionRequest request,
        string permission,
        FormDefinitionStatus toStatus,
        FormReviewDecisionType decisionType,
        string auditAction,
        Func<FormDefinition, Guid, string?, CancellationToken, Task> enforceSod,
        Action<FormDefinition, Guid, DateTimeOffset>? apply,
        CancellationToken cancellationToken)
    {
        FormAccessHelper.EnsurePermission(currentUser, permission);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, cancellationToken: cancellationToken);
        FormAccessHelper.EnsureRowVersion(form.RowVersion, request.RowVersion);
        FormDefinitionStateMachine.EnsureAllowed(form.Status, toStatus);

        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        await enforceSod(form, actorId, null, cancellationToken);

        var from = form.Status;
        var now = DateTimeOffset.UtcNow;
        form.Status = toStatus;
        form.UpdatedAtUtc = now;
        form.UpdatedBy = currentUser.ExternalSubject;
        form.UpdatedByUserId = actorId;
        form.LastModifiedByUserId = actorId;
        apply?.Invoke(form, actorId, now);
        db.Update(form);

        AppendDecision(form.Id, decisionType, from, toStatus, actorId, request.Reason.Trim(), false);
        await audit.WriteAsync(new AuditEntry
        {
            Action = auditAction,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = toStatus },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(form.Id, cancellationToken))!;
    }

    private static void ApplySubmitForReview(FormDefinition form, Guid actorId, DateTimeOffset now)
    {
        form.SubmittedForReviewAtUtc = now;
    }

    private static void ApplyApprove(FormDefinition form, Guid actorId, DateTimeOffset now)
    {
        form.ApprovedAtUtc = now;
    }

    private void AppendDecision(
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
