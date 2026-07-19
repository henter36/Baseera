namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;

public interface INoteWorkflowService
{
    Task<NoteDetailDto> StartWorkAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> SubmitForVerificationAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> ReturnForReworkAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> VerifyClosureAsync(Guid id, CloseNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> ReopenAsync(Guid id, ReopenNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> CancelAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteWorkflowService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAuditService audit,
    INoteQueryService queries) : INoteWorkflowService
{
    public Task<NoteDetailDto> StartWorkAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request.RowVersion,
            PermissionCodes.NotesStartWork,
            NoteStatus.InProgress,
            "NoteWorkStarted",
            request.Reason,
            ApplyStartWork,
            cancellationToken);

    public Task<NoteDetailDto> SubmitForVerificationAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request.RowVersion,
            PermissionCodes.NotesSubmitForVerification,
            NoteStatus.PendingVerification,
            "NoteSubmittedForVerification",
            request.Reason,
            ApplySubmitForVerification,
            cancellationToken);

    public Task<NoteDetailDto> ReturnForReworkAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            id,
            request.RowVersion,
            PermissionCodes.NotesReturnForRework,
            NoteStatus.InProgress,
            "NoteReturnedForRework",
            request.Reason,
            null,
            cancellationToken);

    public async Task<NoteDetailDto> VerifyClosureAsync(Guid id, CloseNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesVerifyClosure);
        var note = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Closed);

        var actorId = RequireUserId();
        EnforceCriticalSoD(note, actorId);

        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Closed;
        note.ClosedAtUtc = now;
        note.ClosedByUserId = actorId;
        note.ClosureSummary = request.ClosureSummary.Trim();
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        CompleteCurrentAssignment(note.Id, now);
        AppendHistory(note.Id, from, NoteStatus.Closed, actorId, request.Reason.Trim());

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteClosed",
            Module = "Notes",
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = NoteStatus.Closed, note.ClosedByUserId, note.ClosureSummary },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    public async Task<NoteDetailDto> ReopenAsync(Guid id, ReopenNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesReopen);
        var note = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Reopened);

        var actorId = RequireUserId();
        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Reopened;
        note.ReopenedAtUtc = now;
        note.ReopenedByUserId = actorId;
        note.ReopenReason = request.Reason.Trim();
        note.ClosedAtUtc = null;
        note.ClosedByUserId = null;
        note.ClosureSummary = null;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        AppendHistory(note.Id, from, NoteStatus.Reopened, actorId, request.Reason.Trim());
        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteReopened",
            Module = "Notes",
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = NoteStatus.Reopened },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    public async Task<NoteDetailDto> CancelAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesCancel);
        var note = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        if (note.Status == NoteStatus.Closed)
        {
            throw new InvalidOperationException("لا يمكن إلغاء ملاحظة مغلقة.");
        }

        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Cancelled);

        var actorId = RequireUserId();
        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Cancelled;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        EndCurrentAssignment(note.Id, now, request.Reason.Trim());
        AppendHistory(note.Id, from, NoteStatus.Cancelled, actorId, request.Reason.Trim());
        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteCancelled",
            Module = "Notes",
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = NoteStatus.Cancelled },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    private async Task<NoteDetailDto> TransitionAsync(
        Guid id,
        string rowVersion,
        string permission,
        NoteStatus toStatus,
        string auditAction,
        string reason,
        Action<OperationalNote, Guid, DateTimeOffset>? apply,
        CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, permission);
        var note = NoteAccessHelper.LoadInScopeOrNotFound(db, noteScope, id);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, rowVersion);

        if (toStatus == NoteStatus.InProgress && note.Status == NoteStatus.Reopened)
        {
            EnsureCurrentAssignmentExists(note.Id);
        }

        NoteStateMachine.EnsureAllowed(note.Status, toStatus);

        var actorId = RequireUserId();
        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = toStatus;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        apply?.Invoke(note, actorId, now);
        db.Update(note);

        AppendHistory(note.Id, from, toStatus, actorId, reason.Trim());
        await audit.WriteAsync(new AuditEntry
        {
            Action = auditAction,
            Module = "Notes",
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = toStatus },
            Reason = reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    private static void ApplyStartWork(OperationalNote note, Guid actorId, DateTimeOffset now)
    {
        note.WorkStartedAtUtc ??= now;
        note.LastProcessedByUserId = actorId;
    }

    private static void ApplySubmitForVerification(OperationalNote note, Guid actorId, DateTimeOffset now)
    {
        note.SubmittedForVerificationAtUtc = now;
        note.LastProcessedByUserId = actorId;
    }

    private void EnforceCriticalSoD(OperationalNote note, Guid closerId)
    {
        if (note.Severity != NoteSeverity.Critical)
        {
            return;
        }

        if (note.LastProcessedByUserId == closerId)
        {
            throw new InvalidOperationException("فصل الواجبات: لا يمكن لمعالج الملاحظة الحرجة اعتماد إغلاقها منفردًا.");
        }
    }

    private void EnsureCurrentAssignmentExists(Guid noteId)
    {
        if (!db.NoteAssignments.Any(a => a.OperationalNoteId == noteId && a.IsCurrent))
        {
            throw new InvalidOperationException("لا يوجد تكليف حالي للانتقال إلى قيد المعالجة.");
        }
    }

    private void CompleteCurrentAssignment(Guid noteId, DateTimeOffset now)
    {
        var current = db.NoteAssignments.FirstOrDefault(a => a.OperationalNoteId == noteId && a.IsCurrent);
        if (current is null)
        {
            return;
        }

        current.CompletedAtUtc = now;
        db.Update(current);
    }

    private void EndCurrentAssignment(Guid noteId, DateTimeOffset now, string reason)
    {
        var current = db.NoteAssignments.FirstOrDefault(a => a.OperationalNoteId == noteId && a.IsCurrent);
        if (current is null)
        {
            return;
        }

        current.IsCurrent = false;
        current.EndedAtUtc = now;
        current.EndReason = reason;
        db.Update(current);
    }

    private void AppendHistory(Guid noteId, NoteStatus? from, NoteStatus to, Guid userId, string? reason)
    {
        db.Add(new NoteStatusHistory
        {
            OperationalNoteId = noteId,
            FromStatus = from,
            ToStatus = to,
            ChangedByUserId = userId,
            ChangedAtUtc = DateTimeOffset.UtcNow,
            Reason = reason
        });
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
