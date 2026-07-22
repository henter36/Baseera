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
    IFormEffectiveAccessService effectiveAccess,
    IAuditService audit,
    IFormQueryService queries) : IFormWorkflowService
{
    private sealed record FormTransitionDefinition(
        string Permission,
        FormAccessCapability Capability,
        FormDefinitionStatus TargetStatus,
        FormReviewDecisionType DecisionType,
        string AuditAction,
        Func<FormDefinition, Guid, string?, CancellationToken, Task> EnforceSeparationOfDuties,
        Action<FormDefinition, Guid, DateTimeOffset>? Apply);

    public Task<FormDetailDto> SubmitForReviewAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            new FormTransitionDefinition(
                PermissionCodes.FormsSubmitForReview,
                FormAccessCapability.Design,
                FormDefinitionStatus.InReview,
                FormReviewDecisionType.SubmitForReview,
                "FormSubmittedForReview",
                sod.EnforceSubmitForReviewAsync,
                ApplySubmitForReview),
            cancellationToken);

    public Task<FormDetailDto> RequestChangesAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            new FormTransitionDefinition(
                PermissionCodes.FormsRequestChanges,
                FormAccessCapability.Review,
                FormDefinitionStatus.ChangesRequested,
                FormReviewDecisionType.RequestChanges,
                "FormChangesRequested",
                sod.EnforceReviewAsync,
                null),
            cancellationToken);

    public Task<FormDetailDto> ApproveAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            new FormTransitionDefinition(
                PermissionCodes.FormsApprove,
                FormAccessCapability.Approve,
                FormDefinitionStatus.Approved,
                FormReviewDecisionType.Approve,
                "FormApproved",
                sod.EnforceApproveAsync,
                ApplyApprove),
            cancellationToken);

    public Task<FormDetailDto> RejectAsync(Guid id, FormTransitionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request,
            new FormTransitionDefinition(
                PermissionCodes.FormsReject,
                FormAccessCapability.Review,
                FormDefinitionStatus.Rejected,
                FormReviewDecisionType.Reject,
                "FormRejected",
                sod.EnforceReviewAsync,
                null),
            cancellationToken);

    private async Task<FormDetailDto> TransitionAsync(
        Guid id,
        FormTransitionRequest request,
        FormTransitionDefinition definition,
        CancellationToken cancellationToken)
    {
        FormAccessHelper.EnsurePermission(currentUser, definition.Permission);
        var form = await FormAccessHelper.LoadInScopeOrNotFoundAsync(db, formScope, id, cancellationToken: cancellationToken);
        await effectiveAccess.EnsureCapabilityAsync(form, definition.Capability, cancellationToken);
        FormAccessHelper.EnsureRowVersion(form.RowVersion, request.RowVersion);
        FormDefinitionStateMachine.EnsureAllowed(form.Status, definition.TargetStatus);

        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        await definition.EnforceSeparationOfDuties(form, actorId, null, cancellationToken);

        var from = form.Status;
        var now = DateTimeOffset.UtcNow;
        form.Status = definition.TargetStatus;
        form.UpdatedAtUtc = now;
        form.UpdatedBy = currentUser.ExternalSubject;
        form.UpdatedByUserId = actorId;
        form.LastModifiedByUserId = actorId;
        definition.Apply?.Invoke(form, actorId, now);
        db.Update(form);

        FormReviewDecisionWriter.Append(
            db,
            new FormReviewDecisionWriteRequest(
                form.Id,
                definition.DecisionType,
                from,
                definition.TargetStatus,
                actorId,
                request.Reason.Trim(),
                false));
        await audit.WriteAsync(new AuditEntry
        {
            Action = definition.AuditAction,
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormDefinition),
            EntityId = form.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = definition.TargetStatus },
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
}
