namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.CorrectiveActions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

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
            new TransitionOptions(
                id,
                request.RowVersion,
                PermissionCodes.NotesStartWork,
                NoteStatus.InProgress,
                "NoteWorkStarted",
                request.Reason,
                ApplyStartWork),
            cancellationToken);

    public Task<NoteDetailDto> SubmitForVerificationAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            new TransitionOptions(
                id,
                request.RowVersion,
                PermissionCodes.NotesSubmitForVerification,
                NoteStatus.PendingVerification,
                "NoteSubmittedForVerification",
                request.Reason,
                ApplySubmitForVerification),
            cancellationToken);

    public Task<NoteDetailDto> ReturnForReworkAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        TransitionAsync(
            new TransitionOptions(
                id,
                request.RowVersion,
                PermissionCodes.NotesReturnForRework,
                NoteStatus.InProgress,
                "NoteReturnedForRework",
                request.Reason,
                null),
            cancellationToken);

    public async Task<NoteDetailDto> VerifyClosureAsync(Guid id, CloseNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesVerifyClosure);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Closed);

        var actorId = RequireUserId();
        await EnforceCriticalSoDAsync(note, actorId, cancellationToken);
        await EnsureNoBlockingCorrectiveActionsAsync(
            note.Id,
            "NoteClosureBlockedByCorrectiveActions",
            "لا يمكن إغلاق الملاحظة لوجود {0} إجراء تصحيحي نشط.",
            cancellationToken);

        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Closed;
        note.ClosedAtUtc = now;
        note.ClosedByUserId = actorId;
        note.ClosureSummary = request.ClosureSummary.Trim();
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await CompleteCurrentAssignmentAsync(note.Id, now, cancellationToken);
        AppendHistory(note.Id, from, NoteStatus.Closed, actorId, request.Reason.Trim());

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteClosed",
            Module = NoteAccessHelper.ModuleName,
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
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
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
            Module = NoteAccessHelper.ModuleName,
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
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        if (note.Status == NoteStatus.Closed)
        {
            throw new InvalidOperationException("لا يمكن إلغاء ملاحظة مغلقة.");
        }

        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Cancelled);
        await EnsureNoBlockingCorrectiveActionsAsync(
            note.Id,
            "NoteCancellationBlockedByCorrectiveActions",
            "لا يمكن إلغاء الملاحظة لوجود {0} إجراء تصحيحي نشط. يجب إكمال الإجراءات أو إلغاؤها بسبب واضح أولًا.",
            cancellationToken);

        var actorId = RequireUserId();
        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = NoteStatus.Cancelled;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await EndCurrentAssignmentAsync(note.Id, now, request.Reason.Trim(), cancellationToken);
        AppendHistory(note.Id, from, NoteStatus.Cancelled, actorId, request.Reason.Trim());
        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteCancelled",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = NoteStatus.Cancelled },
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    private sealed record TransitionOptions(
        Guid Id,
        string RowVersion,
        string Permission,
        NoteStatus ToStatus,
        string AuditAction,
        string Reason,
        Action<OperationalNote, Guid, DateTimeOffset>? Apply);

    private async Task<NoteDetailDto> TransitionAsync(TransitionOptions options, CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, options.Permission);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, options.Id, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, options.RowVersion);

        if (options.ToStatus == NoteStatus.InProgress && note.Status == NoteStatus.Reopened)
        {
            await EnsureCurrentAssignmentExistsAsync(note.Id, cancellationToken);
        }

        NoteStateMachine.EnsureAllowed(note.Status, options.ToStatus);

        var actorId = RequireUserId();
        var from = note.Status;
        var now = DateTimeOffset.UtcNow;
        note.Status = options.ToStatus;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        options.Apply?.Invoke(note, actorId, now);
        db.Update(note);

        AppendHistory(note.Id, from, options.ToStatus, actorId, options.Reason.Trim());
        await audit.WriteAsync(new AuditEntry
        {
            Action = options.AuditAction,
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { Status = from },
            NewValues = new { Status = options.ToStatus },
            Reason = options.Reason.Trim()
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

    /// <summary>
    /// Critical SoD: any user who performed actual processing on this note cannot verify final closure.
    /// Processing is derived from append-only history (not LastProcessedByUserId alone):
    /// Assigned→InProgress, Reopened→InProgress (start-work), InProgress→PendingVerification (submit).
    /// PendingVerification→InProgress (return-for-rework) is NOT processing — typically a reviewer.
    /// </summary>
    private async Task EnforceCriticalSoDAsync(
        OperationalNote note,
        Guid closerId,
        CancellationToken cancellationToken)
    {
        if (note.Severity != NoteSeverity.Critical)
        {
            return;
        }

        var participated = await db.NoteStatusHistories.AnyAsync(
            history =>
                history.OperationalNoteId == note.Id &&
                history.ChangedByUserId == closerId &&
                (
                    (history.FromStatus == NoteStatus.Assigned && history.ToStatus == NoteStatus.InProgress) ||
                    (history.FromStatus == NoteStatus.Reopened && history.ToStatus == NoteStatus.InProgress) ||
                    (history.FromStatus == NoteStatus.InProgress && history.ToStatus == NoteStatus.PendingVerification)
                ),
            cancellationToken);

        if (participated)
        {
            throw new InvalidOperationException(
                "فصل الواجبات: لا يمكن لأي مستخدم شارك في معالجة الملاحظة الحرجة اعتماد إغلاقها النهائي.");
        }
    }

    private async Task EnsureCurrentAssignmentExistsAsync(Guid noteId, CancellationToken cancellationToken)
    {
        if (!await db.NoteAssignments.AnyAsync(a => a.OperationalNoteId == noteId && a.IsCurrent, cancellationToken))
        {
            throw new InvalidOperationException("لا يوجد تكليف حالي للانتقال إلى قيد المعالجة.");
        }
    }

    private async Task EnsureNoBlockingCorrectiveActionsAsync(
        Guid noteId,
        string auditAction,
        string messageTemplate,
        CancellationToken cancellationToken)
    {
        var blockingCount = await db.CorrectiveActions.CountAsync(
            action =>
                action.OperationalNoteId == noteId &&
                action.Status != CorrectiveActionStatus.Completed &&
                action.Status != CorrectiveActionStatus.Cancelled,
            cancellationToken);
        if (blockingCount == 0)
        {
            return;
        }

        await audit.WriteAsync(new AuditEntry
        {
            Action = auditAction,
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = noteId.ToString(),
            NewValues = new { BlockingCorrectiveActions = blockingCount },
            Outcome = "Blocked"
        }, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        throw new InvalidOperationException(string.Format(messageTemplate, blockingCount));
    }

    private async Task CompleteCurrentAssignmentAsync(Guid noteId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var current = await db.NoteAssignments.FirstOrDefaultAsync(a => a.OperationalNoteId == noteId && a.IsCurrent, cancellationToken);
        if (current is null)
        {
            return;
        }

        current.CompletedAtUtc = now;
        db.Update(current);
    }

    private async Task EndCurrentAssignmentAsync(Guid noteId, DateTimeOffset now, string reason, CancellationToken cancellationToken)
    {
        var current = await db.NoteAssignments.FirstOrDefaultAsync(a => a.OperationalNoteId == noteId && a.IsCurrent, cancellationToken);
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
