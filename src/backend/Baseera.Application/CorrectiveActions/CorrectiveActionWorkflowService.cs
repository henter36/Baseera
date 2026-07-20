namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Microsoft.EntityFrameworkCore;

public interface ICorrectiveActionWorkflowService
{
    Task<CorrectiveActionDetailDto> StartWorkAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> SubmitForVerificationAsync(Guid id, CompleteCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> ReturnForReworkAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> VerifyCompletionAsync(Guid id, CompleteCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> ReopenAsync(Guid id, ReopenCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> CancelAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class CorrectiveActionWorkflowService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    ICorrectiveActionScopeService scope,
    IAuditService audit,
    ICorrectiveActionQueryService queries) : ICorrectiveActionWorkflowService
{
    public Task<CorrectiveActionDetailDto> StartWorkAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request.RowVersion,
            PermissionCodes.CorrectiveActionsStartWork,
            CorrectiveActionStatus.InProgress,
            "CorrectiveActionWorkStarted",
            request.Reason,
            ApplyStartWork,
            cancellationToken);

    public async Task<CorrectiveActionDetailDto> SubmitForVerificationAsync(
        Guid id,
        CompleteCorrectiveActionRequest request,
        CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsSubmitForVerification);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        CorrectiveActionStateMachine.EnsureAllowed(action.Status, CorrectiveActionStatus.PendingVerification);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = CorrectiveActionStatus.PendingVerification;
        action.SubmittedForVerificationAtUtc = now;
        action.CompletionSummary = request.CompletionSummary.Trim();
        action.LastProcessedByUserId = actorId;
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, CorrectiveActionStatus.PendingVerification, actorId, request.Reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionSubmittedForVerification", action, new { Status = from }, new { action.Status, action.CompletionSummary }, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public Task<CorrectiveActionDetailDto> ReturnForReworkAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request.RowVersion,
            PermissionCodes.CorrectiveActionsReturnForRework,
            CorrectiveActionStatus.InProgress,
            "CorrectiveActionReturnedForRework",
            request.Reason,
            null,
            cancellationToken);

    public async Task<CorrectiveActionDetailDto> VerifyCompletionAsync(Guid id, CompleteCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsVerifyCompletion);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        CorrectiveActionStateMachine.EnsureAllowed(action.Status, CorrectiveActionStatus.Completed);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        await EnforceCriticalSoDAsync(action, actorId, cancellationToken);

        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = CorrectiveActionStatus.Completed;
        action.CompletedAtUtc = now;
        action.CompletedByUserId = actorId;
        action.CompletionSummary = request.CompletionSummary.Trim();
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        await CompleteCurrentAssignmentAsync(action.Id, now, cancellationToken);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, CorrectiveActionStatus.Completed, actorId, request.Reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionCompleted", action, new { Status = from }, new { action.Status, action.CompletedByUserId, action.CompletionSummary }, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public async Task<CorrectiveActionDetailDto> ReopenAsync(Guid id, ReopenCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsReopen);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        CorrectiveActionStateMachine.EnsureAllowed(action.Status, CorrectiveActionStatus.Reopened);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = CorrectiveActionStatus.Reopened;
        action.ReopenedAtUtc = now;
        action.ReopenedByUserId = actorId;
        action.ReopenReason = request.Reason.Trim();
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, CorrectiveActionStatus.Reopened, actorId, request.Reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionReopened", action, new { Status = from }, new { action.Status }, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public async Task<CorrectiveActionDetailDto> CancelAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsCancel);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        CorrectiveActionStateMachine.EnsureAllowed(action.Status, CorrectiveActionStatus.Cancelled);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = CorrectiveActionStatus.Cancelled;
        action.CancelledAtUtc = now;
        action.CancelledByUserId = actorId;
        action.CancelReason = request.Reason.Trim();
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        await EndCurrentAssignmentAsync(action.Id, now, request.Reason.Trim(), cancellationToken);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, CorrectiveActionStatus.Cancelled, actorId, request.Reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionCancelled", action, new { Status = from }, new { action.Status }, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    private async Task<CorrectiveActionDetailDto> TransitionAsync(
        Guid id,
        string rowVersion,
        string permission,
        CorrectiveActionStatus to,
        string auditAction,
        string reason,
        Action<CorrectiveAction, Guid, DateTimeOffset>? apply,
        CancellationToken cancellationToken)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, permission);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, scope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, rowVersion);
        if (to == CorrectiveActionStatus.InProgress && action.Status == CorrectiveActionStatus.Reopened)
        {
            await EnsureCurrentAssignmentExistsAsync(action.Id, cancellationToken);
        }

        CorrectiveActionStateMachine.EnsureAllowed(action.Status, to);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = to;
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        apply?.Invoke(action, actorId, now);
        db.Update(action);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, to, actorId, reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, auditAction, action, new { Status = from }, new { Status = to }, reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    private static void ApplyStartWork(CorrectiveAction action, Guid actorId, DateTimeOffset now)
    {
        action.WorkStartedAtUtc ??= now;
        action.LastProcessedByUserId = actorId;
    }

    private async Task EnforceCriticalSoDAsync(CorrectiveAction action, Guid verifierId, CancellationToken cancellationToken)
    {
        if (action.Priority != CorrectiveActionPriority.Critical)
        {
            return;
        }

        var participated = await db.CorrectiveActionStatusHistories.AnyAsync(
            history =>
                history.CorrectiveActionId == action.Id &&
                history.ChangedByUserId == verifierId &&
                (
                    (history.FromStatus == CorrectiveActionStatus.Assigned && history.ToStatus == CorrectiveActionStatus.InProgress) ||
                    (history.FromStatus == CorrectiveActionStatus.Reopened && history.ToStatus == CorrectiveActionStatus.InProgress) ||
                    (history.FromStatus == CorrectiveActionStatus.InProgress && history.ToStatus == CorrectiveActionStatus.PendingVerification)
                ),
            cancellationToken);

        if (participated)
        {
            throw new InvalidOperationException("فصل الواجبات: لا يمكن لأي مستخدم شارك في معالجة الإجراء الحرج اعتماد إكماله النهائي.");
        }
    }

    private async Task EnsureCurrentAssignmentExistsAsync(Guid actionId, CancellationToken cancellationToken)
    {
        if (!await db.CorrectiveActionAssignments.AnyAsync(a => a.CorrectiveActionId == actionId && a.IsCurrent, cancellationToken))
        {
            throw new InvalidOperationException("لا يوجد تكليف حالي للانتقال إلى قيد المعالجة.");
        }
    }

    private async Task CompleteCurrentAssignmentAsync(Guid actionId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await db.CorrectiveActionAssignments.FirstOrDefaultAsync(a => a.CorrectiveActionId == actionId && a.IsCurrent, cancellationToken);
        if (current is null) return;
        current.CompletedAtUtc = now;
        db.Update(current);
    }

    private async Task EndCurrentAssignmentAsync(Guid actionId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var current = await db.CorrectiveActionAssignments.FirstOrDefaultAsync(a => a.CorrectiveActionId == actionId && a.IsCurrent, cancellationToken);
        if (current is null) return;
        current.IsCurrent = false;
        current.EndedAtUtc = now;
        current.EndReason = reason;
        db.Update(current);
    }

}
