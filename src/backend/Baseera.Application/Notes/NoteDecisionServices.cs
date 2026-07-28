namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Attachments;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Evidence policy for triage/no-action decisions — server-authored, driven by severity/source,
/// never by client-supplied flags. "اختيارية أو إلزامية حسب التصنيف والخطورة" (spec §أولًا/قرار غير صحيحة).
/// </summary>
public static class NoteEvidencePolicy
{
    public static bool IsEvidenceRequiredForDecision(NoteSeverity severity) =>
        severity is NoteSeverity.High or NoteSeverity.Critical;
}

// ===== Layer 1: Triage gate =====

public interface INoteTriageService
{
    Task<NoteDetailDto> DecideValidAsync(Guid noteId, TriageValidRequest request, CancellationToken cancellationToken = default);
    Task<NoteDecisionApprovalDto> ProposeInvalidAsync(Guid noteId, ProposeInvalidRequest request, CancellationToken cancellationToken = default);
    Task<NoteDecisionApprovalDto> ProposeDuplicateAsync(Guid noteId, ProposeDuplicateRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteTriageService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAttachmentAppService attachments,
    IAuditService audit,
    INoteQueryService queries) : INoteTriageService
{
    public async Task<NoteDetailDto> DecideValidAsync(Guid noteId, TriageValidRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesUpdate);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureAtTriageGate(note);

        var actorId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        note.TriageOutcome = NoteTriageOutcome.Valid;
        note.TriageDecidedAtUtc = now;
        note.TriageDecidedByUserId = actorId;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteTriageDecidedValid",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new { TriageOutcome = NoteTriageOutcome.Valid }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        var detail = await queries.GetDetailAsync(note.Id, cancellationToken);
        return detail ?? throw new InvalidOperationException("تعذر تحميل تفاصيل الملاحظة بعد الحفظ.");
    }

    public async Task<NoteDecisionApprovalDto> ProposeInvalidAsync(Guid noteId, ProposeInvalidRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesProposeInvalid);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureAtTriageGate(note);
        await EnsureEvidenceIfRequiredAsync(note, cancellationToken);
        await EnsureNoPendingApprovalAsync(note.Id, NoteDecisionApprovalType.Invalid, cancellationToken);

        var actorId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        note.TriageOutcome = NoteTriageOutcome.Invalid;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        var approval = new NoteDecisionApproval
        {
            OperationalNoteId = note.Id,
            DecisionType = NoteDecisionApprovalType.Invalid,
            Status = NoteDecisionApprovalStatus.Pending,
            JustificationAr = request.JustificationAr.Trim(),
            ProposedByUserId = actorId,
            ProposedAtUtc = now
        };
        db.Add(approval);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteInvalidProposed",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new { note.TriageOutcome, approval.JustificationAr }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await MapApprovalAsync(approval, cancellationToken);
    }

    public async Task<NoteDecisionApprovalDto> ProposeDuplicateAsync(Guid noteId, ProposeDuplicateRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesProposeDuplicate);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        EnsureAtTriageGate(note);

        if (request.OriginalNoteId == noteId)
        {
            throw new InvalidOperationException("لا يمكن ربط الملاحظة بنفسها كملاحظة أصلية.");
        }

        var original = await db.OperationalNotes.FirstOrDefaultAsync(n => n.Id == request.OriginalNoteId, cancellationToken);
        if (original is null || !noteScope.CanAccess(original))
        {
            throw new KeyNotFoundException("الملاحظة الأصلية غير موجودة أو خارج نطاقك.");
        }

        if (original.NoteTypeId != note.NoteTypeId)
        {
            throw new InvalidOperationException("لا يمكن اعتبار الملاحظة مكررة لملاحظة من نوع مختلف — لمنع الربط العشوائي.");
        }

        var scopeMatches =
            (note.FacilityId.HasValue && original.FacilityId == note.FacilityId) ||
            (!note.FacilityId.HasValue && note.RegionId.HasValue && original.RegionId == note.RegionId) ||
            (!note.FacilityId.HasValue && !note.RegionId.HasValue);
        if (!scopeMatches)
        {
            throw new InvalidOperationException("الملاحظة الأصلية خارج نطاق السجن أو المنطقة — لا يمكن اعتبارها مكررة.");
        }

        await EnsureEvidenceIfRequiredAsync(note, cancellationToken);
        await EnsureNoPendingApprovalAsync(note.Id, NoteDecisionApprovalType.Duplicate, cancellationToken);

        var actorId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        note.TriageOutcome = NoteTriageOutcome.Duplicate;
        note.UpdatedAtUtc = now;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        var approval = new NoteDecisionApproval
        {
            OperationalNoteId = note.Id,
            DecisionType = NoteDecisionApprovalType.Duplicate,
            Status = NoteDecisionApprovalStatus.Pending,
            JustificationAr = request.JustificationAr.Trim(),
            OriginalNoteId = original.Id,
            ProposedByUserId = actorId,
            ProposedAtUtc = now
        };
        db.Add(approval);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteDuplicateProposed",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new { note.TriageOutcome, OriginalNoteId = original.Id }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await MapApprovalAsync(approval, cancellationToken);
    }

    private static void EnsureAtTriageGate(OperationalNote note)
    {
        if (note.Status is NoteStatus.Closed or NoteStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن فرز ملاحظة مغلقة أو ملغاة.");
        }

        if (note.TriageOutcome is not null)
        {
            throw new InvalidOperationException("الملاحظة فُرزت مسبقًا. أعد فتح بوابة الفرز عبر إعادة القرار الحالي أولًا.");
        }
    }

    private async Task EnsureEvidenceIfRequiredAsync(OperationalNote note, CancellationToken cancellationToken)
    {
        if (!NoteEvidencePolicy.IsEvidenceRequiredForDecision(note.Severity))
        {
            return;
        }

        var existing = await attachments.ListForEntityAsync(nameof(OperationalNote), note.Id, cancellationToken);
        if (existing.Count == 0)
        {
            throw new InvalidOperationException("يتطلب تصنيف/خطورة هذه الملاحظة إرفاق دليل مؤيد قبل اتخاذ هذا القرار.");
        }
    }

    private async Task EnsureNoPendingApprovalAsync(Guid noteId, NoteDecisionApprovalType type, CancellationToken cancellationToken)
    {
        var hasPending = await db.NoteDecisionApprovals.AnyAsync(
            a => a.OperationalNoteId == noteId && a.DecisionType == type && a.Status == NoteDecisionApprovalStatus.Pending,
            cancellationToken);
        if (hasPending)
        {
            throw new InvalidOperationException("يوجد بالفعل طلب اعتماد نشط من هذا النوع على الملاحظة.");
        }
    }

    private async Task<NoteDecisionApprovalDto> MapApprovalAsync(NoteDecisionApproval approval, CancellationToken cancellationToken) =>
        await NoteDecisionApprovalMapper.MapAsync(db, approval, cancellationToken);

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}

internal static class NoteDecisionApprovalMapper
{
    public static async Task<NoteDecisionApprovalDto> MapAsync(IBaseeraDbContext db, NoteDecisionApproval approval, CancellationToken cancellationToken)
    {
        var proposedBy = await db.Users.FirstOrDefaultAsync(u => u.Id == approval.ProposedByUserId, cancellationToken);
        var reviewedBy = approval.ReviewedByUserId.HasValue
            ? await db.Users.FirstOrDefaultAsync(u => u.Id == approval.ReviewedByUserId.Value, cancellationToken)
            : null;
        var original = approval.OriginalNoteId.HasValue
            ? await db.OperationalNotesIncludingDeleted.FirstOrDefaultAsync(n => n.Id == approval.OriginalNoteId.Value, cancellationToken)
            : null;

        return new NoteDecisionApprovalDto(
            approval.Id,
            approval.OperationalNoteId,
            approval.DecisionType,
            NoteDisplay.DecisionApprovalTypeAr(approval.DecisionType),
            approval.Status,
            NoteDisplay.DecisionApprovalStatusAr(approval.Status),
            approval.JustificationAr,
            approval.OriginalNoteId,
            original?.ReferenceNumber,
            approval.ProposedByUserId,
            proposedBy?.DisplayNameAr,
            approval.ProposedAtUtc,
            approval.ReviewedByUserId,
            reviewedBy?.DisplayNameAr,
            approval.ReviewedAtUtc,
            approval.ReviewReason,
            Convert.ToBase64String(approval.RowVersion));
    }
}
