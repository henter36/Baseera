namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface ICorrectiveActionAssignmentService
{
    Task<CorrectiveActionDetailDto> AssignAsync(Guid id, AssignCorrectiveActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class CorrectiveActionAssignmentService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    ICorrectiveActionScopeService actionScope,
    INoteTypeAccessService typeAccess,
    IAuditService audit,
    ICorrectiveActionQueryService queries) : ICorrectiveActionAssignmentService
{
    private const string CurrentAssignmentUniqueIndex = "IX_CorrectiveActionAssignments_CorrectiveActionId";
    private static readonly string[] WorkPermissionCodes =
    [
        PermissionCodes.CorrectiveActionsStartWork,
        PermissionCodes.CorrectiveActionsUpdate,
        PermissionCodes.CorrectiveActionsSubmitForVerification
    ];

    public async Task<CorrectiveActionDetailDto> AssignAsync(Guid id, AssignCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsAssign);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, cancellationToken: cancellationToken);
        var note = await db.OperationalNotes.FirstAsync(n => n.Id == action.OperationalNoteId, cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.View, cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        EnsureExactlyOneAssignmentTarget(request);

        if (request.AssignedToUserId is Guid userId)
        {
            await AssignmentTargetValidator.EnsureUserCanReceiveAsync(
                db,
                userId,
                note,
                WorkPermissionCodes,
                "المستخدم لا يملك صلاحية مناسبة للعمل على الإجراء التصحيحي.",
                "نطاق المستخدم لا يتقاطع مع نطاق الملاحظة الأصلية.",
                cancellationToken);
            var assigneeAccess = await typeAccess.GetEffectiveAccessAsync(userId, note.NoteTypeId, cancellationToken);
            if (assigneeAccess?.Process.Allowed != true)
            {
                throw new InvalidOperationException("المستخدم لا يملك صلاحية معالجة نوع الملاحظة الأصلية.");
            }
        }
        else if (request.AssignedToDepartmentId is Guid departmentId)
        {
            await AssignmentTargetValidator.EnsureDepartmentExistsAsync(db, departmentId, cancellationToken);
        }

        var current = await db.CorrectiveActionAssignments
            .FirstOrDefaultAsync(a => a.CorrectiveActionId == id && a.IsCurrent, cancellationToken);
        var isReassign = current is not null;
        EnsureAssignTransition(action.Status, isReassign);

        var actorId = RequireUserId();
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;
        var fromStatus = action.Status;

        if (current is not null)
        {
            current.IsCurrent = false;
            current.EndedAtUtc = now;
            current.EndReason = reason;
            db.Update(current);
        }

        var assignment = new CorrectiveActionAssignment
        {
            CorrectiveActionId = action.Id,
            AssignedToUserId = request.AssignedToUserId,
            AssignedToDepartmentId = request.AssignedToDepartmentId,
            AssignedByUserId = actorId,
            AssignedAtUtc = now,
            DueAtUtc = request.DueAtUtc ?? action.DueAtUtc,
            Reason = reason,
            IsCurrent = true,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(assignment);

        action.Status = CorrectiveActionStatus.Assigned;
        action.UpdatedAtUtc = now;
        action.UpdatedBy = currentUser.ExternalSubject;
        db.Update(action);
        db.Add(new CorrectiveActionStatusHistory
        {
            CorrectiveActionId = action.Id,
            FromStatus = fromStatus,
            ToStatus = CorrectiveActionStatus.Assigned,
            ChangedByUserId = actorId,
            ChangedAtUtc = now,
            Reason = reason,
            AssignmentId = assignment.Id
        });

        await audit.WriteAsync(new AuditEntry
        {
            Action = isReassign ? "CorrectiveActionReassigned" : "CorrectiveActionAssigned",
            Module = CorrectiveActionAccessHelper.ModuleName,
            EntityType = nameof(CorrectiveAction),
            EntityId = action.Id.ToString(),
            OldValues = new { Status = fromStatus, PreviousAssignmentId = current?.Id },
            NewValues = new { Status = CorrectiveActionStatus.Assigned, assignment.AssignedToUserId, assignment.AssignedToDepartmentId, assignment.Id },
            Reason = reason
        }, cancellationToken);

        await SaveAssignmentChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(action.Id, cancellationToken))!;
    }

    private async Task SaveAssignmentChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException("تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.", ex);
        }
        catch (DbUpdateException ex) when (IsCurrentAssignmentUniqueConflict(ex))
        {
            throw new InvalidOperationException("يوجد تكليف حالي بالفعل لهذا الإجراء التصحيحي.", ex);
        }
    }

    private static bool IsCurrentAssignmentUniqueConflict(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains(CurrentAssignmentUniqueIndex, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void EnsureExactlyOneAssignmentTarget(AssignCorrectiveActionRequest request)
    {
        if (request.AssignedToUserId.HasValue == request.AssignedToDepartmentId.HasValue)
        {
            throw new InvalidOperationException("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.");
        }
    }

    private static void EnsureAssignTransition(CorrectiveActionStatus status, bool isReassign)
    {
        if (isReassign && status == CorrectiveActionStatus.Assigned)
        {
            CorrectiveActionStateMachine.EnsureAllowed(CorrectiveActionStatus.Assigned, CorrectiveActionStatus.Assigned);
            return;
        }

        if (status is CorrectiveActionStatus.Open or CorrectiveActionStatus.Reopened)
        {
            CorrectiveActionStateMachine.EnsureAllowed(status, CorrectiveActionStatus.Assigned);
            return;
        }

        throw new InvalidOperationException($"لا يمكن التكليف من الحالة {CorrectiveActionDisplay.StatusAr(status)}.");
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
