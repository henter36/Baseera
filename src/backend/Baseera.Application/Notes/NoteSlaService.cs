namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Three independent SLA clocks (spec §سياسة SLA والتأخر أثناء انتظار القطع): OverallAge never pauses,
/// ProcessingSla pauses only for an approved "بانتظار قطع — انتظار خارجي معتمد" window,
/// ExternalWaitDuration is the sum of those approved windows, reported separately (never blamed on the processor).
/// </summary>
public interface INoteSlaService
{
    Task<NoteSlaStateDto> ComputeAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NoteSlaStateDto> RequestPauseAsync(Guid noteId, RequestSlaPauseRequest request, CancellationToken cancellationToken = default);
    Task<NoteSlaStateDto> ApprovePauseAsync(Guid noteId, Guid pauseId, string rowVersion, CancellationToken cancellationToken = default);
    Task EndPauseIfPartsResolvedAsync(Guid noteId, CancellationToken cancellationToken = default);
}

public sealed class NoteSlaService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IAuditService audit) : INoteSlaService
{
    public async Task<NoteSlaStateDto> ComputeAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        var pauses = await db.NoteSlaPausePeriods.Where(p => p.OperationalNoteId == noteId).ToListAsync(cancellationToken);
        return Compute(note, pauses, DateTimeOffset.UtcNow);
    }

    public async Task<NoteSlaStateDto> RequestPauseAsync(Guid noteId, RequestSlaPauseRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesStartWork);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        if (note.TreatmentExecutionType != NoteTreatmentExecutionType.RequiresParts)
        {
            throw new InvalidOperationException("تجميد SLA متاح فقط لملاحظة بنوع تنفيذ (تتطلب قطع أو مواد).");
        }

        if (request.RelatedPartsRequirementIds.Count == 0)
        {
            throw new InvalidOperationException("يتطلب طلب التجميد ربط عنصر قطعة واحد على الأقل موثّق.");
        }

        var relatedParts = await db.NotePartsRequirements
            .Where(p => p.OperationalNoteId == noteId && request.RelatedPartsRequirementIds.Contains(p.Id))
            .ToListAsync(cancellationToken);
        if (relatedParts.Count != request.RelatedPartsRequirementIds.Count)
        {
            throw new InvalidOperationException("أحد عناصر القطع المرتبطة غير موجود على هذه الملاحظة.");
        }

        if (!relatedParts.Any(p => !string.IsNullOrWhiteSpace(p.RequestNumber)))
        {
            throw new InvalidOperationException("يتطلب طلب التجميد تسجيل رقم طلب توريد على عنصر قطعة واحد على الأقل.");
        }

        if (!relatedParts.Any(p => !string.IsNullOrWhiteSpace(p.SupplierOrSource)))
        {
            throw new InvalidOperationException("يتطلب طلب التجميد تحديد الجهة المسؤولة عن التوريد على عنصر قطعة واحد على الأقل.");
        }

        var hasActive = await db.NoteSlaPausePeriods.AnyAsync(
            p => p.OperationalNoteId == noteId && p.EndedAtUtc == null, cancellationToken);
        if (hasActive)
        {
            throw new InvalidOperationException("يوجد بالفعل طلب تجميد نشط على هذه الملاحظة.");
        }

        var actorId = RequireUserId();
        var now = DateTimeOffset.UtcNow;
        var pause = new NoteSlaPausePeriod
        {
            OperationalNoteId = noteId,
            Reason = request.Reason.Trim(),
            RequestedByUserId = actorId,
            RequestedAtUtc = now,
            ReviewDueAtUtc = request.ReviewDueAtUtc,
            RelatedPartsRequirementIdsCsv = string.Join(",", relatedParts.Select(p => p.Id))
        };
        db.Add(pause);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteSlaPauseRequested",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NoteSlaPausePeriod),
            EntityId = pause.Id.ToString(),
            NewValues = new { noteId, pause.Reason }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ComputeAsync(noteId, cancellationToken);
    }

    public async Task<NoteSlaStateDto> ApprovePauseAsync(Guid noteId, Guid pauseId, string rowVersion, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesApproveSlaPause);
        await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        var pause = await db.NoteSlaPausePeriods.FirstOrDefaultAsync(
            p => p.Id == pauseId && p.OperationalNoteId == noteId, cancellationToken);
        if (pause is null)
        {
            throw new KeyNotFoundException("طلب تجميد SLA غير موجود.");
        }

        if (pause.ApprovedByUserId.HasValue || pause.EndedAtUtc.HasValue)
        {
            throw new InvalidOperationException("طلب التجميد لم يعد بانتظار الاعتماد.");
        }

        NoteAccessHelper.EnsureRowVersion(pause.RowVersion, rowVersion);

        var actorId = RequireUserId();
        if (actorId == pause.RequestedByUserId)
        {
            throw new InvalidOperationException("لا يمكن لمنشئ طلب تجميد SLA اعتماده بنفسه — يلزم مراجع مستقل (Four-eyes).");
        }

        var now = DateTimeOffset.UtcNow;
        pause.ApprovedByUserId = actorId;
        pause.ApprovedAtUtc = now;
        pause.StartedAtUtc = now;
        db.Update(pause);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteSlaPauseApproved",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NoteSlaPausePeriod),
            EntityId = pause.Id.ToString(),
            NewValues = new { pause.StartedAtUtc }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return await ComputeAsync(noteId, cancellationToken);
    }

    public async Task EndPauseIfPartsResolvedAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        var pause = await db.NoteSlaPausePeriods.FirstOrDefaultAsync(
            p => p.OperationalNoteId == noteId && p.EndedAtUtc == null && p.ApprovedByUserId != null, cancellationToken);
        if (pause is null)
        {
            return;
        }

        var parts = await db.NotePartsRequirements.Where(p => p.OperationalNoteId == noteId).ToListAsync(cancellationToken);
        var progress = NotePartsRequirementService.ComputeProgress(parts);
        if (!progress.AllResolved)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        pause.EndedAtUtc = now;
        pause.EndReason = "اكتمال جميع القطع الفعالة أو إلغاؤها";
        db.Update(pause);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteSlaPauseEnded",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NoteSlaPausePeriod),
            EntityId = pause.Id.ToString(),
            Reason = pause.EndReason
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public static NoteSlaStateDto Compute(OperationalNote note, IReadOnlyList<NoteSlaPausePeriod> pauses, DateTimeOffset now)
    {
        var boundEnd = note.ClosedAtUtc ?? now;
        var overallStart = note.SubmittedAtUtc ?? note.CreatedAtUtc;
        var overallAge = boundEnd - overallStart;

        var approvedPauses = pauses.Where(p => p.StartedAtUtc.HasValue).ToList();
        var externalWait = TimeSpan.Zero;
        foreach (var pause in approvedPauses)
        {
            if (pause.StartedAtUtc is not { } startedAt)
            {
                continue;
            }

            var pauseEnd = pause.EndedAtUtc ?? boundEnd;
            if (pauseEnd > startedAt)
            {
                externalWait += pauseEnd - startedAt;
            }
        }

        var processingSla = TimeSpan.Zero;
        if (note.WorkStartedAtUtc.HasValue)
        {
            var rawProcessing = boundEnd - note.WorkStartedAtUtc.Value;
            processingSla = rawProcessing - externalWait;
            if (processingSla < TimeSpan.Zero)
            {
                processingSla = TimeSpan.Zero;
            }
        }

        var activePause = pauses.FirstOrDefault(p => p.ApprovedByUserId.HasValue && p.EndedAtUtc is null);

        return new NoteSlaStateDto(
            overallAge.TotalSeconds,
            processingSla.TotalSeconds,
            externalWait.TotalSeconds,
            activePause is not null,
            activePause?.Id,
            activePause?.StartedAtUtc,
            activePause?.Reason,
            activePause?.ReviewDueAtUtc);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
