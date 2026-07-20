namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.Common;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface ICorrectiveActionCommandService
{
    Task<CorrectiveActionDetailDto> CreateDraftAsync(Guid noteId, CreateCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> UpdateAsync(Guid id, UpdateCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task<CorrectiveActionDetailDto> SubmitAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class CorrectiveActionCommandService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    ICorrectiveActionScopeService actionScope,
    IAuditService audit,
    ICorrectiveActionQueryService queries) : ICorrectiveActionCommandService
{
    private static readonly NoteStatus[] CreatableNoteStatuses =
    [
        NoteStatus.Open,
        NoteStatus.Assigned,
        NoteStatus.InProgress,
        NoteStatus.PendingVerification,
        NoteStatus.Reopened
    ];

    public async Task<CorrectiveActionDetailDto> CreateDraftAsync(
        Guid noteId,
        CreateCorrectiveActionRequest request,
        CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsCreate);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var note = await db.OperationalNotes.FirstOrDefaultAsync(n => n.Id == noteId, cancellationToken)
            ?? throw new KeyNotFoundException("الملاحظة غير موجودة.");
        if (!noteScope.CanAccess(note))
        {
            throw new KeyNotFoundException("الملاحظة غير موجودة.");
        }

        if (!CreatableNoteStatuses.Contains(note.Status))
        {
            throw new InvalidOperationException("لا يمكن إنشاء إجراء تصحيحي لهذه الحالة من الملاحظة.");
        }

        if (request.OwnerDepartmentId.HasValue &&
            !await db.Departments.AnyAsync(d => d.Id == request.OwnerDepartmentId.Value && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }

        var classification = request.Classification ?? note.Classification;
        if (classification < note.Classification)
        {
            throw new InvalidOperationException("تصنيف الإجراء التصحيحي لا يمكن أن يكون أقل من تصنيف الملاحظة.");
        }

        var now = DateTimeOffset.UtcNow;
        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value < now)
        {
            throw new InvalidOperationException("تاريخ الاستحقاق لا يمكن أن يسبق تاريخ إنشاء الإجراء.");
        }

        var sequence = await db.NextCorrectiveActionSequenceValueAsync(cancellationToken);
        var action = new CorrectiveAction
        {
            ReferenceNumber = CorrectiveActionReferenceFormatter.Format(sequence),
            OperationalNoteId = note.Id,
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            Priority = request.Priority,
            Status = CorrectiveActionStatus.Draft,
            Classification = classification,
            OwnerDepartmentId = request.OwnerDepartmentId,
            CreatedByUserId = actorId,
            CreatedAtUtc = now,
            DueAtUtc = request.DueAtUtc,
            CreatedBy = currentUser.ExternalSubject
        };

        db.Add(action);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, null, CorrectiveActionStatus.Draft, actorId, "إنشاء مسودة");
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionCreated", action, null, new { action.ReferenceNumber, action.OperationalNoteId, action.Priority, action.Classification }, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public async Task<CorrectiveActionDetailDto> UpdateAsync(Guid id, UpdateCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsUpdate);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        var note = await db.OperationalNotes.FirstAsync(n => n.Id == action.OperationalNoteId, cancellationToken);

        if (action.Status is CorrectiveActionStatus.Completed or CorrectiveActionStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن تعديل إجراء مكتمل أو ملغى إلا عبر انتقال صريح.");
        }

        if (request.Classification < note.Classification)
        {
            throw new InvalidOperationException("تصنيف الإجراء التصحيحي لا يمكن أن يكون أقل من تصنيف الملاحظة.");
        }

        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value < action.CreatedAtUtc)
        {
            throw new InvalidOperationException("تاريخ الاستحقاق لا يمكن أن يسبق تاريخ إنشاء الإجراء.");
        }

        var old = new { action.Title, action.Description, action.Priority, action.Classification, action.OwnerDepartmentId, action.DueAtUtc };
        action.Title = request.Title.Trim();
        action.Description = request.Description.Trim();
        action.Priority = request.Priority;
        action.Classification = request.Classification;
        action.OwnerDepartmentId = request.OwnerDepartmentId;
        action.DueAtUtc = request.DueAtUtc;
        action.UpdatedAtUtc = DateTimeOffset.UtcNow;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionUpdated", action, old, new { action.Title, action.Description, action.Priority, action.Classification, action.OwnerDepartmentId, action.DueAtUtc }, "تحديث حقول الإجراء", cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public async Task<CorrectiveActionDetailDto> SubmitAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsUpdate);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        CorrectiveActionStateMachine.EnsureAllowed(action.Status, CorrectiveActionStatus.Open);
        var actorId = CorrectiveActionServiceSupport.RequireUserId(currentUser);
        var from = action.Status;
        var now = DateTimeOffset.UtcNow;
        action.Status = CorrectiveActionStatus.Open;
        action.SubmittedAtUtc = now;
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        CorrectiveActionServiceSupport.AppendHistory(db, action.Id, from, CorrectiveActionStatus.Open, actorId, request.Reason.Trim());
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionSubmitted", action, new { Status = from }, new { Status = CorrectiveActionStatus.Open }, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    public async Task ArchiveAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsArchive);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        action.IsDeleted = true;
        action.DeletedAtUtc = DateTimeOffset.UtcNow;
        action.DeletedBy = currentUser.ExternalSubject;
        action.UpdatedAtUtc = action.DeletedAtUtc;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionArchived", action, null, null, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, TransitionCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsRestore);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, includeDeleted: true, cancellationToken: cancellationToken);
        if (!action.IsDeleted)
        {
            throw new InvalidOperationException("الإجراء التصحيحي غير مؤرشف.");
        }

        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        action.IsDeleted = false;
        action.DeletedAtUtc = null;
        action.DeletedBy = null;
        action.UpdatedAtUtc = DateTimeOffset.UtcNow;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        await CorrectiveActionServiceSupport.WriteAuditAsync(audit, "CorrectiveActionRestored", action, null, null, request.Reason.Trim(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

}
