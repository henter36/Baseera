namespace Baseera.Application.CorrectiveActions;

using Baseera.Application.Abstractions;
using Baseera.Application.Notes;
using Baseera.Domain.Common;
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
    IAuditService audit,
    ICorrectiveActionQueryService queries) : ICorrectiveActionAssignmentService
{
    private const string CurrentAssignmentUniqueIndex = "IX_CorrectiveActionAssignments_CorrectiveActionId";

    public async Task<CorrectiveActionDetailDto> AssignAsync(Guid id, AssignCorrectiveActionRequest request, CancellationToken cancellationToken = default)
    {
        CorrectiveActionAccessHelper.EnsurePermission(currentUser, PermissionCodes.CorrectiveActionsAssign);
        var action = await CorrectiveActionAccessHelper.LoadInScopeOrNotFoundAsync(db, actionScope, id, cancellationToken: cancellationToken);
        CorrectiveActionAccessHelper.EnsureRowVersion(action.RowVersion, request.RowVersion);
        EnsureExactlyOneAssignmentTarget(request);

        var note = await db.OperationalNotes.FirstAsync(n => n.Id == action.OperationalNoteId, cancellationToken);
        if (request.AssignedToUserId is Guid userId)
        {
            await ValidateAssigneeUserAsync(userId, note, cancellationToken);
        }
        else if (request.AssignedToDepartmentId is Guid departmentId)
        {
            await ValidateAssigneeDepartmentAsync(departmentId, cancellationToken);
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

    private async Task ValidateAssigneeUserAsync(Guid userId, OperationalNote note, CancellationToken cancellationToken)
    {
        var user = await db.UsersIncludingDeleted.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null || user.IsDeleted)
        {
            throw new KeyNotFoundException("المستخدم غير موجود.");
        }

        if (!user.IsActive || user.ProvisioningStatus != UserProvisioningStatus.Active)
        {
            throw new InvalidOperationException("المستخدم غير نشط أو غير مُهيّأ لاستلام التكليف.");
        }

        await EnsureAssigneeCanWorkAsync(userId, cancellationToken);
        await EnsureAssigneeScopeIntersectsAsync(userId, note, cancellationToken);
    }

    private async Task ValidateAssigneeDepartmentAsync(Guid departmentId, CancellationToken cancellationToken)
    {
        if (!await db.Departments.AnyAsync(d => d.Id == departmentId && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }
    }

    private async Task EnsureAssigneeCanWorkAsync(Guid userId, CancellationToken cancellationToken)
    {
        var workPermissions = new[]
        {
            PermissionCodes.CorrectiveActionsStartWork,
            PermissionCodes.CorrectiveActionsUpdate,
            PermissionCodes.CorrectiveActionsSubmitForVerification
        };

        var hasWork = await (
            from ur in db.UserRoles
            join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && workPermissions.Contains(p.Code)
            select p.Code).AnyAsync(cancellationToken);

        if (!hasWork)
        {
            throw new InvalidOperationException("المستخدم لا يملك صلاحية مناسبة للعمل على الإجراء التصحيحي.");
        }
    }

    private async Task EnsureAssigneeScopeIntersectsAsync(Guid userId, OperationalNote note, CancellationToken cancellationToken)
    {
        var scopes = await db.UserScopes
            .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
            .Select(s => new UserScopeSnapshot(s.ScopeType, s.RegionId, s.FacilityId, s.FacilityUnitId))
            .ToListAsync(cancellationToken);

        if (scopes.Count == 0)
        {
            throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة الأصلية.");
        }

        if (scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return;
        }

        if (await NoteAssigneeScopeIntersection.IntersectsAsync(db, scopes, note, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة الأصلية.");
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
