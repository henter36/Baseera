namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;

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
    public async Task<NoteDetailDto> AssignAsync(Guid id, AssignNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesAssign);
        var note = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;

        if (request.AssignedToUserId.HasValue)
        {
            await ValidateAssigneeUserAsync(request.AssignedToUserId.Value, note, cancellationToken);
        }
        else
        {
            ValidateAssigneeDepartment(request.AssignedToDepartmentId!.Value);
        }

        var current = db.NoteAssignments.FirstOrDefault(a => a.OperationalNoteId == id && a.IsCurrent);
        var isReassign = current is not null;
        var fromStatus = note.Status;

        if (current is not null)
        {
            current.IsCurrent = false;
            current.EndedAtUtc = now;
            current.EndReason = reason;
            db.Update(current);
        }

        EnsureAssignTransition(note.Status, isReassign);

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

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
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

    private Task ValidateAssigneeUserAsync(Guid userId, OperationalNote note, CancellationToken cancellationToken)
    {
        var user = db.UsersIncludingDeleted.FirstOrDefault(u => u.Id == userId);
        if (user is null || user.IsDeleted)
        {
            throw new KeyNotFoundException("المستخدم غير موجود.");
        }

        if (!user.IsActive || user.ProvisioningStatus != UserProvisioningStatus.Active)
        {
            throw new InvalidOperationException("المستخدم غير نشط أو غير مُهيّأ لاستلام التكليف.");
        }

        EnsureAssigneeCanWork(userId);
        EnsureAssigneeScopeIntersects(userId, note);
        return Task.CompletedTask;
    }

    private void ValidateAssigneeDepartment(Guid departmentId)
    {
        if (!db.Departments.Any(d => d.Id == departmentId && !d.IsDeleted))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }
    }

    private void EnsureAssigneeCanWork(Guid userId)
    {
        var workPermissions = new[]
        {
            PermissionCodes.NotesStartWork,
            PermissionCodes.NotesUpdate,
            PermissionCodes.NotesSubmitForVerification
        };

        var hasWork = (
            from ur in db.UserRoles
            join rp in db.RolePermissions on ur.RoleId equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where ur.UserId == userId && workPermissions.Contains(p.Code)
            select p.Code).Any();

        if (!hasWork)
        {
            throw new InvalidOperationException("المستخدم لا يملك صلاحية مناسبة للعمل على الملاحظة.");
        }
    }

    private void EnsureAssigneeScopeIntersects(Guid userId, OperationalNote note)
    {
        var scopes = db.UserScopes
            .Where(s => s.UserId == userId && s.IsActive && !s.IsDeleted)
            .Select(s => new UserScopeSnapshot(s.ScopeType, s.RegionId, s.FacilityId, s.FacilityUnitId))
            .ToList();

        if (scopes.Count == 0)
        {
            throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة.");
        }

        if (scopes.Any(s => s.ScopeType == ScopeType.Global))
        {
            return;
        }

        if (note.ScopeType == ScopeType.Headquarters &&
            scopes.Any(s => s.ScopeType is ScopeType.Headquarters or ScopeType.Global))
        {
            return;
        }

        if (IntersectsNote(scopes, note))
        {
            return;
        }

        throw new InvalidOperationException("نطاق المستخدم لا يتقاطع مع نطاق الملاحظة.");
    }

    private bool IntersectsNote(IReadOnlyList<UserScopeSnapshot> scopes, OperationalNote note)
    {
        return note.ScopeType switch
        {
            ScopeType.Region => note.RegionId is Guid rid && scopes.Any(s =>
                (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions && s.RegionId == rid) ||
                (s.FacilityId.HasValue && db.Facilities.Any(f => f.Id == s.FacilityId && f.RegionId == rid))),
            ScopeType.Facility => note.FacilityId is Guid fid && scopes.Any(s =>
                (s.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities or ScopeType.FacilityUnit && s.FacilityId == fid) ||
                (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions && s.RegionId.HasValue &&
                 db.Facilities.Any(f => f.Id == fid && f.RegionId == s.RegionId))),
            ScopeType.FacilityUnit => note.FacilityUnitId is Guid uid && (
                scopes.Any(s => s.ScopeType == ScopeType.FacilityUnit && s.FacilityUnitId == uid) ||
                (note.FacilityId is Guid fid && scopes.Any(s =>
                    (s.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities && s.FacilityId == fid) ||
                    (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions && s.RegionId.HasValue &&
                     db.Facilities.Any(f => f.Id == fid && f.RegionId == s.RegionId))))),
            ScopeType.Global => scopes.Any(s => s.ScopeType == ScopeType.Global),
            ScopeType.Headquarters => scopes.Any(s => s.ScopeType is ScopeType.Headquarters or ScopeType.Global),
            _ => false
        };
    }
}
