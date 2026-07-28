namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Unified four-eyes engine for the three decision types that close a note without ever routing
/// through PendingVerification (Invalid/Duplicate/NoAction). One model for all three
/// (docs/ux-rescue/phase1b-observation-architecture.md §مسارات الاعتماد) — approve/return here,
/// type-specific finalization (closure reason, duplicate link) dispatched from one switch.
/// </summary>
public interface INoteDecisionApprovalService
{
    Task<IReadOnlyList<NoteDecisionApprovalDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> ApproveAsync(Guid noteId, Guid approvalId, ApproveNoteDecisionRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> ReturnAsync(Guid noteId, Guid approvalId, ReturnNoteDecisionRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteDecisionApprovalService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAuditService audit,
    INoteQueryService queries) : INoteDecisionApprovalService
{
    public async Task<IReadOnlyList<NoteDecisionApprovalDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);

        var rows = await db.NoteDecisionApprovals
            .Where(a => a.OperationalNoteId == noteId)
            .OrderByDescending(a => a.ProposedAtUtc)
            .ToListAsync(cancellationToken);

        var result = new List<NoteDecisionApprovalDto>(rows.Count);
        foreach (var row in rows)
        {
            result.Add(await NoteDecisionApprovalMapper.MapAsync(db, row, cancellationToken));
        }

        return result;
    }

    public async Task<NoteDetailDto> ApproveAsync(Guid noteId, Guid approvalId, ApproveNoteDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var (note, approval) = await LoadPendingAsync(noteId, approvalId, cancellationToken);
        EnsurePermissionForType(approval.DecisionType, approve: true);
        NoteAccessHelper.EnsureRowVersion(approval.RowVersion, request.RowVersion);

        var actorId = RequireUserId();
        if (actorId == approval.ProposedByUserId)
        {
            throw new InvalidOperationException("لا يمكن لمن اقترح القرار اعتماده — يلزم مراجع مستقل (Four-eyes).");
        }

        var now = DateTimeOffset.UtcNow;
        var fromStatus = note.Status;
        approval.Status = NoteDecisionApprovalStatus.Approved;
        approval.ReviewedByUserId = actorId;
        approval.ReviewedAtUtc = now;
        approval.ReviewReason = string.IsNullOrWhiteSpace(request.ReviewReason) ? null : request.ReviewReason.Trim();
        db.Update(approval);

        ApplyClosureForApprovedDecision(note, approval, now);
        db.Update(note);

        db.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = fromStatus,
            ToStatus = NoteStatus.Closed,
            ChangedByUserId = actorId,
            ChangedAtUtc = now,
            Reason = $"اعتماد {NoteDisplay.DecisionApprovalTypeAr(approval.DecisionType)}",
            MetadataJson = $"{{\"decisionType\":\"{approval.DecisionType}\",\"approvalId\":\"{approval.Id}\"}}"
        });

        await audit.WriteAsync(new AuditEntry
        {
            Action = $"Note{approval.DecisionType}Approved",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = new { approval.ProposedByUserId },
            NewValues = new { ReviewedByUserId = actorId, note.ClosureReason, note.DuplicateOfNoteId }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await LoadDetailOrThrowAsync(note.Id, cancellationToken);
    }

    public async Task<NoteDetailDto> ReturnAsync(Guid noteId, Guid approvalId, ReturnNoteDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var (note, approval) = await LoadPendingAsync(noteId, approvalId, cancellationToken);
        EnsurePermissionForType(approval.DecisionType, approve: true);
        NoteAccessHelper.EnsureRowVersion(approval.RowVersion, request.RowVersion);

        if (string.IsNullOrWhiteSpace(request.ReviewReason))
        {
            throw new InvalidOperationException("سبب إعادة القرار مطلوب.");
        }

        var actorId = RequireUserId();
        if (actorId == approval.ProposedByUserId)
        {
            throw new InvalidOperationException("لا يمكن لمن اقترح القرار إعادته لنفسه — يلزم مراجع مستقل (Four-eyes).");
        }

        var now = DateTimeOffset.UtcNow;
        approval.Status = NoteDecisionApprovalStatus.Returned;
        approval.ReviewedByUserId = actorId;
        approval.ReviewedAtUtc = now;
        approval.ReviewReason = request.ReviewReason.Trim();
        db.Update(approval);

        // Returned decisions send the note back to the triage gate — it never disappeared from
        // follow-up in the meantime (spec: "لا تختفي الملاحظة من المتابعة قبل الاعتماد النهائي").
        note.TriageOutcome = null;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = $"Note{approval.DecisionType}Returned",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            Reason = approval.ReviewReason
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await LoadDetailOrThrowAsync(note.Id, cancellationToken);
    }

    private async Task<NoteDetailDto> LoadDetailOrThrowAsync(Guid noteId, CancellationToken cancellationToken)
    {
        var detail = await queries.GetDetailAsync(noteId, cancellationToken);
        return detail ?? throw new InvalidOperationException("تعذر تحميل تفاصيل الملاحظة بعد الحفظ.");
    }

    private void ApplyClosureForApprovedDecision(OperationalNote note, NoteDecisionApproval approval, DateTimeOffset now)
    {
        NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Closed);
        note.Status = NoteStatus.Closed;
        note.ClosedAtUtc = now;
        note.ClosedByUserId = approval.ReviewedByUserId;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;

        switch (approval.DecisionType)
        {
            case NoteDecisionApprovalType.Invalid:
                note.ClosureReason = NoteClosureReason.Invalid;
                note.ClosureSummary = approval.JustificationAr;
                break;
            case NoteDecisionApprovalType.Duplicate:
                note.ClosureReason = NoteClosureReason.Duplicate;
                note.DuplicateOfNoteId = approval.OriginalNoteId;
                note.ClosureSummary = approval.JustificationAr;
                break;
            case NoteDecisionApprovalType.NoAction:
                note.ClosureReason = NoteClosureReason.NoActionRequired;
                note.ClosureSummary = approval.JustificationAr;
                break;
        }
    }

    private async Task<(OperationalNote Note, NoteDecisionApproval Approval)> LoadPendingAsync(
        Guid noteId,
        Guid approvalId,
        CancellationToken cancellationToken)
    {
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        var approval = await db.NoteDecisionApprovals.FirstOrDefaultAsync(
            a => a.Id == approvalId && a.OperationalNoteId == noteId, cancellationToken);
        if (approval is null)
        {
            throw new KeyNotFoundException("طلب الاعتماد غير موجود.");
        }

        if (approval.Status != NoteDecisionApprovalStatus.Pending)
        {
            throw new InvalidOperationException("طلب الاعتماد لم يعد بانتظار المراجعة.");
        }

        return (note, approval);
    }

    private void EnsurePermissionForType(NoteDecisionApprovalType type, bool approve)
    {
        var permission = (type, approve) switch
        {
            (NoteDecisionApprovalType.Invalid, true) => PermissionCodes.NotesApproveInvalid,
            (NoteDecisionApprovalType.Duplicate, true) => PermissionCodes.NotesApproveDuplicate,
            (NoteDecisionApprovalType.NoAction, true) => PermissionCodes.NotesApproveNoAction,
            _ => throw new InvalidOperationException("نوع قرار غير مدعوم.")
        };
        NoteAccessHelper.EnsurePermission(currentUser, permission);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
