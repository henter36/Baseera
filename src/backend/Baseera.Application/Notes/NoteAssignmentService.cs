namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
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
    INoteTypeAccessService typeAccess,
    IAuditService audit,
    INoteQueryService queries) : INoteAssignmentService
{
    private const string CurrentAssignmentUniqueIndex = "IX_NoteAssignments_OperationalNoteId";
    private static readonly string[] WorkPermissionCodes =
    [
        PermissionCodes.NotesStartWork,
        PermissionCodes.NotesUpdate,
        PermissionCodes.NotesSubmitForVerification
    ];

    public async Task<NoteDetailDto> AssignAsync(Guid id, AssignNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesAssign);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Assign, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureExactlyOneAssignmentTarget(request);

        var actorId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        var reason = request.Reason.Trim();
        var now = DateTimeOffset.UtcNow;

        if (request.AssignedToUserId is Guid userId)
        {
            await AssignmentTargetValidator.EnsureUserCanReceiveAsync(
                db,
                userId,
                note,
                WorkPermissionCodes,
                "المستخدم لا يملك صلاحية مناسبة للعمل على الملاحظة.",
                "نطاق المستخدم لا يتقاطع مع نطاق الملاحظة.",
                cancellationToken);
            if (!await UserHasTypeCapabilityAsync(userId, note.NoteTypeId, NoteTypeCapability.Process, cancellationToken))
            {
                throw new InvalidOperationException("المستخدم لا يملك صلاحية معالجة نوع الملاحظة.");
            }
        }
        else if (request.AssignedToDepartmentId is Guid departmentId)
        {
            await AssignmentTargetValidator.EnsureDepartmentExistsAsync(db, departmentId, cancellationToken);
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

    private async Task<bool> UserHasTypeCapabilityAsync(Guid userId, Guid noteTypeId, NoteTypeCapability capability, CancellationToken cancellationToken)
    {
        var access = await typeAccess.GetEffectiveAccessAsync(userId, noteTypeId, cancellationToken);
        return capability switch
        {
            NoteTypeCapability.Process => access?.Process.Allowed == true,
            NoteTypeCapability.Review => access?.Review.Allowed == true,
            _ => access?.View.Allowed == true
        };
    }

}
