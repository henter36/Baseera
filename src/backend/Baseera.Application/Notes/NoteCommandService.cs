namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteCommandService
{
    Task<NoteDetailDto> CreateDraftAsync(CreateNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteDetailDto> SubmitAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task ArchiveAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task RestoreAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
}

public sealed class NoteCommandService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    INoteScopeService noteScope,
    IOrganizationalScopeService orgScope,
    INoteTypeAccessService typeAccess,
    INoteRoutingService routing,
    IAuditService audit,
    INoteQueryService queries) : INoteCommandService
{
    public async Task<NoteDetailDto> CreateDraftAsync(CreateNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesCreate);
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

        await EnsureCanCreateTypeAsync(request.NoteTypeId, cancellationToken);
        var intake = await ResolveIntakeAsync(userId, request.RegionId, request.FacilityId, cancellationToken);

        noteScope.ValidateScopeShape(ScopeType.Facility, intake.RegionId, intake.FacilityId, null);
        await noteScope.EnsureOrgEntitiesActiveAsync(
            ScopeType.Facility, intake.RegionId, intake.FacilityId, null, cancellationToken);

        var probe = new OperationalNote
        {
            ScopeType = ScopeType.Facility,
            RegionId = intake.RegionId,
            FacilityId = intake.FacilityId,
            FacilityUnitId = null
        };
        if (!noteScope.CanAccess(probe))
        {
            throw new UnauthorizedAccessException("لا صلاحية على نطاق هذه الملاحظة.");
        }

        if (request.OwnerDepartmentId.HasValue &&
            !await db.Departments.AnyAsync(d => d.Id == request.OwnerDepartmentId.Value && !d.IsDeleted, cancellationToken))
        {
            throw new KeyNotFoundException("الإدارة غير موجودة.");
        }

        var sequence = await db.NextOperationalNoteSequenceValueAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var note = new OperationalNote
        {
            ReferenceNumber = NoteReferenceFormatter.Format(sequence),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            NoteTypeId = request.NoteTypeId,
            Severity = request.Severity,
            Status = NoteStatus.Draft,
            SourceType = request.SourceType,
            SourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? null : request.SourceReference.Trim(),
            Classification = request.Classification,
            ScopeType = ScopeType.Facility,
            RegionId = intake.RegionId,
            FacilityId = intake.FacilityId,
            FacilityUnitId = null,
            OwnerDepartmentId = request.OwnerDepartmentId,
            ReportedByUserId = userId,
            ReportedAtUtc = now,
            DueAtUtc = request.DueAtUtc,
            CreatedBy = currentUser.ExternalSubject
        };

        db.Add(note);
        db.Add(new NoteStatusHistory
        {
            OperationalNoteId = note.Id,
            FromStatus = null,
            ToStatus = NoteStatus.Draft,
            ChangedByUserId = userId,
            ChangedAtUtc = now,
            Reason = "إنشاء مسودة"
        });

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteCreated",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            NewValues = new
            {
                note.ReferenceNumber,
                note.Title,
                note.Status,
                note.Severity,
                note.NoteTypeId,
                note.Classification,
                note.ScopeType,
                note.RegionId,
                note.FacilityId,
                note.FacilityUnitId
            }
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    public async Task<NoteDetailDto> UpdateAsync(Guid id, UpdateNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesUpdate);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.View, cancellationToken);
        await EnsureCanCreateTypeAsync(request.NoteTypeId, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        if (note.Status is NoteStatus.Closed or NoteStatus.Cancelled)
        {
            throw new InvalidOperationException("لا يمكن تعديل ملاحظة مغلقة أو ملغاة إلا عبر انتقال صريح.");
        }

        var old = new
        {
            note.Title,
            note.Description,
            note.NoteTypeId,
            note.Severity,
            note.SourceType,
            note.SourceReference,
            note.Classification,
            note.OwnerDepartmentId,
            note.DueAtUtc
        };
        note.Title = request.Title.Trim();
        note.Description = request.Description.Trim();
        note.NoteTypeId = request.NoteTypeId;
        note.Severity = request.Severity;
        note.SourceType = request.SourceType;
        note.SourceReference = string.IsNullOrWhiteSpace(request.SourceReference) ? null : request.SourceReference.Trim();
        note.Classification = request.Classification;
        note.OwnerDepartmentId = request.OwnerDepartmentId;
        note.DueAtUtc = request.DueAtUtc;
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteUpdated",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            OldValues = old,
            NewValues = new
            {
                note.Title,
                note.Description,
                note.NoteTypeId,
                note.Severity,
                note.SourceType,
                note.SourceReference,
                note.Classification,
                note.OwnerDepartmentId,
                note.DueAtUtc
            },
            Reason = "تحديث حقول الملاحظة"
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
        return (await queries.GetDetailAsync(note.Id, cancellationToken))!;
    }

    public async Task<NoteDetailDto> SubmitAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        var noteId = await db.ExecuteInTransactionAsync(
            async ct =>
            {
                NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesUpdate);
                var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: ct);
                await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.View, ct);
                NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
                NoteStateMachine.EnsureAllowed(note.Status, NoteStatus.Open);

                var userId = RequireUserId();
                var from = note.Status;
                note.Status = NoteStatus.Open;
                note.SubmittedAtUtc = DateTimeOffset.UtcNow;
                note.UpdatedAtUtc = note.SubmittedAtUtc;
                note.UpdatedBy = currentUser.ExternalSubject;
                db.Update(note);

                AppendHistory(note.Id, from, NoteStatus.Open, userId, request.Reason.Trim());
                await audit.WriteAsync(new AuditEntry
                {
                    Action = "NoteSubmitted",
                    Module = NoteAccessHelper.ModuleName,
                    EntityType = nameof(OperationalNote),
                    EntityId = note.Id.ToString(),
                    OldValues = new { Status = from },
                    NewValues = new { Status = NoteStatus.Open },
                    Reason = request.Reason.Trim()
                }, ct);

                await routing.RouteOnSubmitAsync(note, note.SubmittedAtUtc.Value, request.Reason, ct);
                await db.SaveChangesAsync(ct);
                return note.Id;
            },
            cancellationToken);

        return (await queries.GetDetailAsync(noteId, cancellationToken))!;
    }

    public async Task ArchiveAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesArchive);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Archive, cancellationToken);
        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);

        note.IsDeleted = true;
        note.DeletedAtUtc = DateTimeOffset.UtcNow;
        note.DeletedBy = currentUser.ExternalSubject;
        note.UpdatedAtUtc = note.DeletedAtUtc;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteArchived",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task RestoreAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default)
    {
        NoteAccessHelper.EnsurePermission(currentUser, PermissionCodes.NotesRestore);
        var note = await NoteAccessHelper.LoadInScopeOrNotFoundAsync(db, noteScope, id, includeDeleted: true, cancellationToken: cancellationToken);
        await typeAccess.EnsureCanAsync(note.NoteTypeId, NoteTypeCapability.Restore, cancellationToken);
        if (!note.IsDeleted)
        {
            throw new InvalidOperationException("الملاحظة غير مؤرشفة.");
        }

        NoteAccessHelper.EnsureRowVersion(note.RowVersion, request.RowVersion);
        note.IsDeleted = false;
        note.DeletedAtUtc = null;
        note.DeletedBy = null;
        note.UpdatedAtUtc = DateTimeOffset.UtcNow;
        note.UpdatedBy = currentUser.ExternalSubject;
        db.Update(note);

        await audit.WriteAsync(new AuditEntry
        {
            Action = "NoteRestored",
            Module = NoteAccessHelper.ModuleName,
            EntityType = nameof(OperationalNote),
            EntityId = note.Id.ToString(),
            Reason = request.Reason.Trim()
        }, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

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

    private async Task EnsureCanCreateTypeAsync(Guid noteTypeId, CancellationToken cancellationToken)
    {
        var type = await db.NoteTypes.FirstOrDefaultAsync(t => t.Id == noteTypeId, cancellationToken)
            ?? throw new KeyNotFoundException("نوع الملاحظة غير موجود.");
        if (!type.IsActive)
        {
            throw new InvalidOperationException("نوع الملاحظة غير فعال للإنشاء.");
        }

        await typeAccess.EnsureCanAsync(noteTypeId, NoteTypeCapability.Create, cancellationToken);
    }

    private async Task<(Guid RegionId, Guid FacilityId)> ResolveIntakeAsync(Guid userId, Guid? requestedRegionId, Guid? requestedFacilityId, CancellationToken cancellationToken)
    {
        if (!requestedRegionId.HasValue || !requestedFacilityId.HasValue)
        {
            throw new InvalidOperationException("يجب اختيار المنطقة ثم الموقع.");
        }

        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == requestedFacilityId.Value && f.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("الموقع غير موجود.");
        if (facility.RegionId != requestedRegionId.Value)
        {
            throw new InvalidOperationException("الموقع لا يتبع المنطقة المحددة.");
        }

        var probe = new OperationalNote { ScopeType = ScopeType.Facility, RegionId = facility.RegionId, FacilityId = facility.Id };
        if (!orgScope.CanAccess(probe))
        {
            throw new UnauthorizedAccessException("الموقع خارج نطاق المستخدم.");
        }

        var profile = await db.UserNoteIntakeProfiles.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
        if (profile?.LockType == NoteIntakeLockType.Region && profile.RegionId != facility.RegionId)
        {
            throw new InvalidOperationException("المنطقة المحددة لا تطابق منطقة الإدخال المثبتة.");
        }

        if (profile?.LockType == NoteIntakeLockType.Facility && profile.FacilityId != facility.Id)
        {
            throw new InvalidOperationException("الموقع المحدد لا يطابق موقع الإدخال المثبت.");
        }

        return (facility.RegionId, facility.Id);
    }
}
