namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
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
    INoteTypeAccessService typeAccess,
    IAuditService audit,
    INoteQueryService queries,
    IAttachmentAppService attachments) : INoteWorkflowService
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
                ApplySubmitForVerification,
                EnsureTreatmentReadyForVerificationAsync),
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
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Review, cancellationToken);
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
        // This is always the "معالجة" outcome — Invalid/Duplicate/NoAction close via
        // NoteDecisionApprovalService instead (never through VerifyClosure/PendingVerification).
        note.ClosureReason = NoteClosureReason.Treated;
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
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Reopen, cancellationToken);
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
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Cancel, cancellationToken);
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
        Action<OperationalNote, Guid, DateTimeOffset>? Apply,
        Func<OperationalNote, CancellationToken, Task>? ValidateAsync = null);

    private async Task<NoteDetailDto> TransitionAsync(TransitionOptions options, CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, options.Permission);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, options.Id, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, CapabilityForPermission(options.Permission), cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, options.RowVersion);

        if (options.ValidateAsync is not null)
        {
            await options.ValidateAsync(note, cancellationToken);
        }

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

    private static NoteTypeCapability CapabilityForPermission(string permission) => permission switch
    {
        PermissionCodes.NotesStartWork => NoteTypeCapability.Process,
        PermissionCodes.NotesSubmitForVerification => NoteTypeCapability.SubmitForVerification,
        PermissionCodes.NotesReturnForRework => NoteTypeCapability.Review,
        _ => NoteTypeCapability.View
    };

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
    /// SoD on final closure: any user who performed actual processing on this note cannot verify it.
    /// Processing is derived from append-only history (not LastProcessedByUserId alone):
    /// Assigned→InProgress, Reopened→InProgress (start-work), InProgress→PendingVerification (submit).
    /// PendingVerification→InProgress (return-for-rework) is NOT processing — typically a reviewer.
    /// Runs for NoteSeverity.Critical (original scope, unchanged) OR whenever this note went through the
    /// Phase 1B treatment-result flow (TreatmentResultType=Treated) — the spec's general four-eyes
    /// requirement on "نتيجة المعالجة" closure, widened here rather than built as a second parallel
    /// check, and provably inert for every pre-Phase-1B note/test (TreatmentResultType stays null
    /// unless RecordTreatmentResultAsync was called).
    /// </summary>
    private async Task EnforceCriticalSoDAsync(
        OperationalNote note,
        Guid closerId,
        CancellationToken cancellationToken)
    {
        if (note.Severity != NoteSeverity.Critical && note.TreatmentResultType != NoteTreatmentResultType.Treated)
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

    /// <summary>
    /// Gate for REQUEST_VERIFICATION (spec §معالجة تتطلب قطعًا أو مواد/نتيجة المعالجة): blocks
    /// submit-for-verification until a "معالجة" result is recorded, its text is filled in, all active
    /// parts are installed/exempted (when execution type requires parts), and — for High/Critical —
    /// supporting evidence exists. A no-op for every pre-Phase-1B note (TreatmentResultType stays null).
    /// </summary>
    private async Task EnsureTreatmentReadyForVerificationAsync(OperationalNote note, CancellationToken cancellationToken)
    {
        if (note.TreatmentResultType != NoteTreatmentResultType.Treated)
        {
            throw new InvalidOperationException("يجب تسجيل نتيجة معالجة (معالجة) قبل إرسال الملاحظة للتحقق.");
        }

        if (string.IsNullOrWhiteSpace(note.TreatmentResultText))
        {
            throw new InvalidOperationException("نتيجة المعالجة النصية مطلوبة قبل إرسال الملاحظة للتحقق.");
        }

        if (note.TreatmentExecutionType == NoteTreatmentExecutionType.RequiresParts)
        {
            var parts = await db.NotePartsRequirements.Where(p => p.OperationalNoteId == note.Id).ToListAsync(cancellationToken);
            var progress = NotePartsRequirementService.ComputeProgress(parts);
            if (!progress.AllResolved)
            {
                throw new InvalidOperationException("لا يمكن إرسال المعالجة للتحقق قبل اكتمال جميع القطع الفعالة (تم التركيب) أو إعفائها.");
            }
        }

        if (NoteEvidencePolicy.IsEvidenceRequiredForDecision(note.Severity))
        {
            var existing = await attachments.ListForEntityAsync(nameof(OperationalNote), note.Id, cancellationToken);
            if (existing.Count == 0)
            {
                throw new InvalidOperationException("يتطلب تصنيف/خطورة هذه الملاحظة إرفاق دليل مؤيد قبل إرسال المعالجة للتحقق.");
            }
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
