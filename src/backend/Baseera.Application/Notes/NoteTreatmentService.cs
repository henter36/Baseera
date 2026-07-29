namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Layer 2 (نتيجة المعالجة) — only reachable once TriageOutcome=Valid. Recording a treatment result
/// never itself changes NoteStatus; the existing SubmitForVerification/VerifyClosure transitions
/// (NoteWorkflowService) remain the only path to PendingVerification/Closed for the "معالجة" outcome.
/// </summary>
public interface INoteTreatmentService
{
    Task<NoteDetailDto> RecordTreatmentResultAsync(Guid noteId, RecordTreatmentResultRequest request, CancellationToken cancellationToken = default);
    Task<NoteDecisionApprovalDto> ProposeNoActionAsync(Guid noteId, ProposeNoActionRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteTreatmentService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    INoteTypeAccessService typeAccess,
    IAttachmentAppService attachments,
    IAuditService audit,
    INoteQueryService queries) : INoteTreatmentService
{
    public async Task<NoteDetailDto> RecordTreatmentResultAsync(Guid noteId, RecordTreatmentResultRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesStartWork);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Process, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureValidAndOpenForTreatment(note);

        if (request.ExecutionType == NoteTreatmentExecutionType.RequiresParts)
        {
            var supportsParts = await db.NoteTypes
                .Where(t => t.Id == note.NoteTypeId)
                .Select(t => t.SupportsPartsWorkflow)
                .FirstOrDefaultAsync(cancellationToken);
            if (!supportsParts)
            {
                throw new InvalidOperationException("نوع هذه الملاحظة لا يسمح بمسار (تتطلب قطع أو مواد).");
            }
        }

        var now = DateTimeOffset.UtcNow;
        note.TreatmentResultType = NoteTreatmentResultType.Treated;
        note.TreatmentExecutionType = request.ExecutionType;
        note.TreatmentResultText = request.TreatmentResultText.Trim();
        note.LastProcessedByUserId = RequireUserId();
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteTreatmentResultRecorded",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new { note.TreatmentExecutionType }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        var detail = await queries.GetDetailAsync(note.Id, cancellationToken);
        return detail ?? throw new InvalidOperationException("تعذر تحميل تفاصيل الملاحظة بعد الحفظ.");
    }

    public async Task<NoteDecisionApprovalDto> ProposeNoActionAsync(Guid noteId, ProposeNoActionRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesProposeNoAction);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Process, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureValidAndOpenForTreatment(note);

        if (NoteEvidencePolicy.IsEvidenceRequiredForDecision(note.Severity))
        {
            var existing = await attachments.ListForEntityAsync(nameof(OperationalNote), note.Id, cancellationToken);
            if (existing.Count == 0)
            {
                throw new InvalidOperationException("يتطلب تصنيف/خطورة هذه الملاحظة إرفاق دليل مؤيد قبل اقتراح عدم الحاجة إلى إجراء.");
            }
        }

        var hasPending = await db.NoteDecisionApprovals.AnyAsync(
            a => a.OperationalNoteId == note.Id && a.DecisionType == NoteDecisionApprovalType.NoAction && a.Status == NoteDecisionApprovalStatus.Pending,
            cancellationToken);
        if (hasPending)
        {
            throw new InvalidOperationException("يوجد بالفعل طلب اعتماد (لا تتطلب إجراء) نشط على الملاحظة.");
        }

        var actorId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        note.TreatmentResultType = NoteTreatmentResultType.NoActionRequired;
        note.NoActionJustificationAr = request.JustificationAr.Trim();
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        var approval = new NoteDecisionApproval
        {
            OperationalNoteId = note.Id,
            DecisionType = NoteDecisionApprovalType.NoAction,
            Status = NoteDecisionApprovalStatus.Pending,
            JustificationAr = request.JustificationAr.Trim(),
            ProposedByUserId = actorId,
            ProposedAtUtc = now
        };
        db.Add(approval);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteNoActionProposed",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new { note.TreatmentResultType }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await NoteDecisionApprovalMapper.MapAsync(db, approval, cancellationToken);
    }

    private static void EnsureValidAndOpenForTreatment(OperationalNote note)
    {
        if (note.TriageOutcome != NoteTriageOutcome.Valid)
        {
            throw new InvalidOperationException("نتيجة المعالجة لا تظهر إلا بعد اعتماد الملاحظة كـ(صحيحة).");
        }

        if (note.Status is NoteStatus.Closed or NoteStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن تسجيل نتيجة معالجة لملاحظة مغلقة أو ملغاة.");
        }
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
