namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// PartsRequirement[] — real multiplicity (spec §معالجة تتطلب قطعًا أو مواد). One row per part/material;
/// no single-part fields, no per-part note, no per-part state machine (bounded status enum instead).
/// </summary>
public interface INotePartsRequirementService
{
    Task<IReadOnlyList<NotePartsRequirementDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NotePartsProgressDto> GetProgressAsync(Guid noteId, CancellationToken cancellationToken = default);
    Task<NotePartsRequirementDto> AddAsync(Guid noteId, AddPartsRequirementRequest request, CancellationToken cancellationToken = default);
    Task<NotePartsRequirementDto> UpdateAsync(Guid noteId, Guid itemId, UpdatePartsRequirementRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid noteId, Guid itemId, CancellationToken cancellationToken = default);
    Task<NotePartsRequirementDto> UpdateStatusAsync(Guid noteId, Guid itemId, UpdatePartsRequirementStatusRequest request, CancellationToken cancellationToken = default);
    Task<NotePartsRequirementDto> CancelAsync(Guid noteId, Guid itemId, CancelPartsRequirementRequest request, CancellationToken cancellationToken = default);
}

public sealed class NotePartsRequirementService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    INoteTypeAccessService typeAccess,
    IAuditService audit,
    INoteSlaService sla) : INotePartsRequirementService
{
    public async Task<IReadOnlyList<NotePartsRequirementDto>> ListAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        var rows = await db.NotePartsRequirements
            .Where(p => p.OperationalNoteId == noteId)
            .OrderBy(p => p.RequestedAtUtc)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<NotePartsProgressDto> GetProgressAsync(Guid noteId, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesView);
        await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        var rows = await db.NotePartsRequirements.Where(p => p.OperationalNoteId == noteId).ToListAsync(cancellationToken);
        return ComputeProgress(rows);
    }

    public async Task<NotePartsRequirementDto> AddAsync(Guid noteId, AddPartsRequirementRequest request, CancellationToken cancellationToken = default)
    {
        var note = await LoadEditableNoteAsync(noteId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ItemCode))
        {
            var duplicate = await db.NotePartsRequirements.AnyAsync(
                p => p.OperationalNoteId == noteId && p.ItemCode == request.ItemCode && p.Status != NotePartsRequirementStatus.Cancelled,
                cancellationToken);
            if (duplicate)
            {
                throw new InvalidOperationException("يوجد بالفعل عنصر نشط بنفس رمز القطعة على هذه الملاحظة.");
            }
        }

        var now = DateTimeOffset.UtcNow;
        var item = new NotePartsRequirement
        {
            OperationalNoteId = noteId,
            ItemName = request.ItemName.Trim(),
            ItemCode = string.IsNullOrWhiteSpace(request.ItemCode) ? null : request.ItemCode.Trim(),
            Quantity = request.Quantity,
            Unit = request.Unit.Trim(),
            RequestNumber = string.IsNullOrWhiteSpace(request.RequestNumber) ? null : request.RequestNumber.Trim(),
            Status = NotePartsRequirementStatus.Requested,
            RequestedAtUtc = now,
            SupplierOrSource = request.SupplierOrSource,
            Notes = request.Notes,
            CreatedByUserId = RequireUserId()
        };
        db.Add(item);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NotePartsRequirementAdded",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NotePartsRequirement),
            EntityId = item.Id.ToString(),
            NewValues = new { note.Id, item.ItemName, item.Quantity, item.Unit }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task<NotePartsRequirementDto> UpdateAsync(Guid noteId, Guid itemId, UpdatePartsRequirementRequest request, CancellationToken cancellationToken = default)
    {
        await LoadEditableNoteAsync(noteId, cancellationToken);
        var item = await LoadItemAsync(noteId, itemId, cancellationToken);
        EnsureEditable(item);
        NoteAccessHelper.EnsureRowVersion(item.RowVersion, request.RowVersion);

        item.ItemName = request.ItemName.Trim();
        item.ItemCode = string.IsNullOrWhiteSpace(request.ItemCode) ? null : request.ItemCode.Trim();
        item.Quantity = request.Quantity;
        item.Unit = request.Unit.Trim();
        item.RequestNumber = string.IsNullOrWhiteSpace(request.RequestNumber) ? null : request.RequestNumber.Trim();
        item.SupplierOrSource = request.SupplierOrSource;
        item.Notes = request.Notes;
        db.Update(item);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NotePartsRequirementUpdated",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NotePartsRequirement),
            EntityId = item.Id.ToString(),
            NewValues = new { item.ItemName, item.Quantity, item.Unit }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return Map(item);
    }

    public async Task DeleteAsync(Guid noteId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var note = await LoadEditableNoteAsync(noteId, cancellationToken);
        if (note.Status != NoteStatus.InProgress)
        {
            throw new InvalidOperationException("لا يمكن حذف عنصر بعد إرسال المعالجة للتحقق — استخدم الإلغاء بسبب بدلًا من ذلك.");
        }

        var item = await LoadItemAsync(noteId, itemId, cancellationToken);
        EnsureEditable(item);
        db.Remove(item);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NotePartsRequirementDeleted",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(NotePartsRequirement),
            EntityId = item.Id.ToString()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task<NotePartsRequirementDto> UpdateStatusAsync(Guid noteId, Guid itemId, UpdatePartsRequirementStatusRequest request, CancellationToken cancellationToken = default) =>
        // Single transaction: the part-status write and the SLA-pause-end check it can trigger must
        // commit together, or a failure in the second write would leave ProcessingSla paused while
        // the part is already Installed/Cancelled (CodeRabbit: corrupts SLA metrics on partial failure).
        db.ExecuteInTransactionAsync(async ct =>
        {
            await LoadEditableNoteAsync(noteId, ct);
            var item = await LoadItemAsync(noteId, itemId, ct);
            NoteAccessHelper.EnsureRowVersion(item.RowVersion, request.RowVersion);
            EnsureForwardTransition(item.Status, request.Status);

            var now = DateTimeOffset.UtcNow;
            item.Status = request.Status;
            switch (request.Status)
            {
                case NotePartsRequirementStatus.Available:
                    item.AvailableAtUtc ??= now;
                    break;
                case NotePartsRequirementStatus.Received:
                    item.ReceivedAtUtc ??= now;
                    break;
                case NotePartsRequirementStatus.Installed:
                    item.InstalledAtUtc ??= now;
                    break;
            }
            db.Update(item);

            await audit.WriteAsync(new AuditEntry
            {
                Action = "NotePartsRequirementStatusChanged",
                Module = NoteAccessHelper.ModuleName,
                EntityType = nameof(NotePartsRequirement),
                EntityId = item.Id.ToString(),
                NewValues = new { item.Status }
            }, ct);

            await db.SaveChangesAsync(ct);
            await sla.EndPauseIfPartsResolvedAsync(noteId, ct);
            return Map(item);
        }, cancellationToken);

    public Task<NotePartsRequirementDto> CancelAsync(Guid noteId, Guid itemId, CancelPartsRequirementRequest request, CancellationToken cancellationToken = default) =>
        db.ExecuteInTransactionAsync(async ct =>
        {
            await LoadEditableNoteAsync(noteId, ct);
            var item = await LoadItemAsync(noteId, itemId, ct);
            NoteAccessHelper.EnsureRowVersion(item.RowVersion, request.RowVersion);
            if (item.Status is NotePartsRequirementStatus.Cancelled or NotePartsRequirementStatus.Installed)
            {
                throw new InvalidOperationException("لا يمكن إلغاء عنصر تم تركيبه أو ملغى مسبقًا.");
            }

            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                throw new InvalidOperationException("سبب الإلغاء مطلوب.");
            }

            var now = DateTimeOffset.UtcNow;
            item.Status = NotePartsRequirementStatus.Cancelled;
            item.CancelledAtUtc = now;
            item.CancelledByUserId = RequireUserId();
            item.CancelReason = request.Reason.Trim();
            db.Update(item);

            await audit.WriteAsync(new AuditEntry
            {
                Action = "NotePartsRequirementCancelled",
                Module = NoteAccessHelper.ModuleName,
                EntityType = nameof(NotePartsRequirement),
                EntityId = item.Id.ToString(),
                Reason = item.CancelReason
            }, ct);

            await db.SaveChangesAsync(ct);
            await sla.EndPauseIfPartsResolvedAsync(noteId, ct);
            return Map(item);
        }, cancellationToken);

    public static NotePartsProgressDto ComputeProgress(IReadOnlyList<NotePartsRequirement> items)
    {
        var active = items.Where(i => i.Status != NotePartsRequirementStatus.Cancelled).ToList();
        var installed = active.Count(i => i.Status == NotePartsRequirementStatus.Installed);
        var cancelled = items.Count(i => i.Status == NotePartsRequirementStatus.Cancelled);
        var remaining = active.Count - installed;
        return new NotePartsProgressDto(items.Count, installed, cancelled, remaining, active.Count > 0 && remaining == 0);
    }

    private static void EnsureForwardTransition(NotePartsRequirementStatus from, NotePartsRequirementStatus to)
    {
        if (from == NotePartsRequirementStatus.Cancelled || from == NotePartsRequirementStatus.Installed)
        {
            throw new InvalidOperationException("لا يمكن تغيير حالة عنصر تم تركيبه أو ملغى.");
        }

        if (to == NotePartsRequirementStatus.Cancelled)
        {
            throw new InvalidOperationException("استخدم إجراء الإلغاء المخصص (يتطلب سببًا).");
        }

        if ((int)to < (int)from)
        {
            throw new InvalidOperationException("لا يمكن إرجاع حالة القطعة إلى مرحلة سابقة.");
        }
    }

    private static void EnsureEditable(NotePartsRequirement item)
    {
        if (item.Status is NotePartsRequirementStatus.Installed or NotePartsRequirementStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن تعديل أو حذف عنصر تم تركيبه أو ملغى.");
        }
    }

    private async Task<OperationalNote> LoadEditableNoteAsync(Guid noteId, CancellationToken cancellationToken)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesStartWork);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, noteId, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Process, cancellationToken);
        if (note.TreatmentExecutionType != NoteTreatmentExecutionType.RequiresParts)
        {
            throw new InvalidOperationException("متطلبات القطع متاحة فقط عند اختيار نوع تنفيذ (تتطلب قطع أو مواد).");
        }

        if (note.Status is NoteStatus.Closed or NoteStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن إدارة القطع لملاحظة مغلقة أو ملغاة.");
        }

        return note;
    }

    private async Task<NotePartsRequirement> LoadItemAsync(Guid noteId, Guid itemId, CancellationToken cancellationToken)
    {
        var item = await db.NotePartsRequirements.FirstOrDefaultAsync(
            p => p.Id == itemId && p.OperationalNoteId == noteId, cancellationToken);
        return item ?? throw new KeyNotFoundException("عنصر القطعة غير موجود.");
    }

    private static NotePartsRequirementDto Map(NotePartsRequirement item) => new(
        item.Id,
        item.OperationalNoteId,
        item.ItemName,
        item.ItemCode,
        item.Quantity,
        item.Unit,
        item.RequestNumber,
        item.Status,
        NoteDisplay.PartsRequirementStatusAr(item.Status),
        item.RequestedAtUtc,
        item.AvailableAtUtc,
        item.ReceivedAtUtc,
        item.InstalledAtUtc,
        item.SupplierOrSource,
        item.Notes,
        item.CancelReason,
        Convert.ToBase64String(item.RowVersion));

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
}
