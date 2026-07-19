namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteAssignmentService
{
    Task<NoteDetailDto> AssignAsync(Guid id, AssignNoteRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteAssignmentService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAuditService audit,
    INoteQueryService queries) : INoteAssignmentService
{
    private const string CurrentAssignmentUniqueIndex = "IX_NoteAssignments_OperationalNoteId";

    public async Task<NoteDetailDto> AssignAsync(Guid id, AssignNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesAssign);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureExactlyOneAssignmentTarget(request);

        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;

        if (request.AssignedToUserId is Guid userId)
        {
            await ValidateAssigneeUserAsync(userId, note, cancellationToken);
        }
        else if (request.AssignedToDepartmentId is Guid departmentId)
        {
            await ValidateAssigneeDepartmentAsync(departmentId, cancellationToken);
        }

        var current = await db.NoteAssignments
            .FirstOrDefaultAsync(a => a.OperationalNoteId == id && a.IsCurrent, cancellationToken);
        var isReassign = current is not null;
        var fromStatus = note.Status;

        // Validate transition before mutating any tracked assignment/note entities.
        EnsureAssignTransition(note.Status, isReassign);

        if (current is not null)
        {
            current.IsCurrent = false;
            current.EndedAtUtc = now;
            current.EndReason = reason;
            db.Update(current);
        }

        var assignment = new NoteAssignment
        {
            OperationalNoteId = note.Id,
            AssignedToUserId = request.AssignedToUserId,
            AssignedToDepartmentId = request.AssignedToDepartmentId,
            AssignedByUserId = actorId,
            AssignedAtUtc = now,
            DueAtUtc = request.DueAtUtc ?? note.DueAtUtc,
            Reason = reason,
            IsCurrent = true,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(assignment);

        note.Status = NoteStatus.Assigned;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        db.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = fromStatus,
            ToStatus = NoteStatus.Assigned,
            ChangedByUserId = actorId,
            ChangedAtUtc = now,
            Reason = reason,
            AssignmentId = assignment.Id
        });

        await audit.WriteAsync(new AuditEntry
        {
            Action = isReassign ? "NoteReassigned" : "NoteAssigned",
            Module = "Notes",
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = fromStatus, PreviousAssignmentId = current?.Id },
            NewValues = new
            {
                Status = NoteStatus.Assigned,
                assignment.AssignedToUserId,
                assignment.AssignedToDepartmentId,
                assignment.Id
            },
            Reason = reason
        }, cancellationToken);

        await SaveAssignmentChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    private async Task SaveAssignmentChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new InvalidOperationException(
                "تم تعديل السجل بواسطة مستخدم آخر. أعد التحميل ثم حاول مجددًا.",
                ex);
        }
        catch (DbUpdateException ex) when (IsCurrentAssignmentUniqueConflict(ex))
        {
            throw new InvalidOperationException(
                "يوجد تكليف حالي بالفعل لهذه الملاحظة.",
                ex);
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

    private static void EnsureExactlyOneAssignmentTarget(AssignNoteRequest request)
    {
        var hasUser = request.AssignedToUserId.HasValue;
        var hasDepartment = request.AssignedToDepartmentId.HasValue;

        if (hasUser == hasDepartment)
        {
            throw new InvalidOperationException("يجب تحديد مستخدم أو إدارة واحدة فقط للتكليف.");
        }
    }

    private static void EnsureAssignTransition(NoteStatus status, bool isReassign)
    {
        if (isReassign && status == NoteStatus.Assigned)
        {
            NoteStateMachine.EnsureAllowed(NoteStatus.Assigned, NoteStatus.Assigned);
            return;
        }

        if (status is NoteStatus.Open or NoteStatus.Reopened)
        {
            NoteStateMachine.EnsureAllowed(status, NoteStatus.Assigned);
            return;
        }

        throw new InvalidOperationException($"لا يمكن التكليف من الحالة {NoteDisplay.StatusAr(status)}.");
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
            PermissionCodes.NotesStartWork,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesSubmitForVerification
        };

        var hasWork = await (
            from ur in db.UserRoles
            join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && workPermissions.Contains(p.Code)
            select p.Code).AnyAsync(cancellationToken);

        if (!hasWork)
        {
            throw new InvalidOperationException("المستخدم لا يملك صلاحية مناسبة للعمل على الملاحظة.");
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
            throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة.");
        }

        // Global short-circuit: IntersectsAsync does not treat Global as universal for
        // Region/Facility/FacilityUnit notes; keep this gate so Global assignees remain universal.
        if (scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return;
        }

        if (await NoteAssigneeScopeIntersection.IntersectsAsync(db, scopes, note, cancellationToken))
        {
            return;
        }

        throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة.");
    }
}
