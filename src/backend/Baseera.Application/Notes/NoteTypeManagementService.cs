namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteTypeManagementService
{
    Task<IReadOnlyList<NoteTypeDto>> ListNoteTypesAsync(bool includeInactive = true, CancellationToken cancellationToken = default);
    Task<NoteTypeDto?> GetNoteTypeAsync(Guid id, CancellationToken cancellationToken = default);
    Task<NoteTypeDto> CreateNoteTypeAsync(CreateNoteTypeRequest request, CancellationToken cancellationToken = default);
    Task<NoteTypeDto> UpdateNoteTypeAsync(Guid id, UpdateNoteTypeRequest request, CancellationToken cancellationToken = default);
    Task<NoteTypeDto> ActivateNoteTypeAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<NoteTypeDto> DeactivateNoteTypeAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleNoteTypeGrantDto>> GetRoleGrantsAsync(Guid roleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RoleNoteTypeGrantDto>> ReplaceRoleGrantsAsync(Guid roleId, ReplaceRoleNoteTypeGrantsRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserNoteTypeOverrideDto>> GetUserOverridesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserNoteTypeOverrideDto>> ReplaceUserOverridesAsync(Guid userId, ReplaceUserNoteTypeOverridesRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserNoteIntakeProfileDto> GetIntakeProfileAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<UserNoteIntakeProfileDto> UpdateIntakeProfileAsync(Guid userId, UpdateUserNoteIntakeProfileRequest request, CancellationToken cancellationToken = default);
    Task<NoteIntakeContextDto> GetMyIntakeContextAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteIntakeFacilityDto>> GetMyIntakeFacilitiesAsync(Guid regionId, CancellationToken cancellationToken = default);
}

public sealed class NoteTypeManagementService(
    IBaseeraDbContext db,
    ICurrentUser currentUser,
    IOrganizationalScopeService orgScope,
    INoteTypeAccessService access,
    IAuditService audit) : INoteTypeManagementService
{
    private const string NoteTypeNotFoundMessage = "نوع الملاحظة غير موجود.";

    public async Task<IReadOnlyList<NoteTypeDto>> ListNoteTypesAsync(bool includeInactive = true, CancellationToken cancellationToken = default)
    {
        EnsureAny(PermissionCodes.NotesManageTypes, PermissionCodes.NotesView);
        var query = db.NoteTypes.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(type => type.IsActive);
        }

        var noteTypes = await query
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.NameAr)
            .ToListAsync(cancellationToken);

        return noteTypes.Select(ToDto).ToList();
    }

    public async Task<NoteTypeDto?> GetNoteTypeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        EnsureAny(PermissionCodes.NotesManageTypes, PermissionCodes.NotesView);
        var type = await db.NoteTypes.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        return type is null ? null : ToDto(type);
    }

    public async Task<NoteTypeDto> CreateNoteTypeAsync(CreateNoteTypeRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageTypes);
        var userId = currentUser.UserId;
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.NoteTypes.AnyAsync(t => t.Code == code, cancellationToken))
        {
            throw new InvalidOperationException("رمز نوع الملاحظة مستخدم مسبقًا.");
        }

        var type = new NoteType
        {
            Code = code,
            NameAr = request.NameAr.Trim(),
            DescriptionAr = TrimOrNull(request.DescriptionAr),
            EntryInstructionsAr = TrimOrNull(request.EntryInstructionsAr),
            SortOrder = request.SortOrder,
            IsActive = request.IsActive,
            DefaultSeverity = request.DefaultSeverity,
            DefaultDueDays = request.DefaultDueDays,
            CreatedByUserId = userId,
            CreatedBy = currentUser.ExternalSubject
        };
        db.Add(type);
        await WriteAuditAsync("NoteTypeCreated", nameof(NoteType), type.Id, new { type.Code, type.NameAr }, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    public async Task<NoteTypeDto> UpdateNoteTypeAsync(Guid id, UpdateNoteTypeRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageTypes);
        var type = await db.NoteTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw new KeyNotFoundException(NoteTypeNotFoundMessage);
        NoteAccessHelper.EnsureRowVersion(type.RowVersion, request.RowVersion);
        var old = new { type.NameAr, type.DescriptionAr, type.EntryInstructionsAr, type.SortOrder, type.DefaultSeverity, type.DefaultDueDays };
        type.NameAr = request.NameAr.Trim();
        type.DescriptionAr = TrimOrNull(request.DescriptionAr);
        type.EntryInstructionsAr = TrimOrNull(request.EntryInstructionsAr);
        type.SortOrder = request.SortOrder;
        type.DefaultSeverity = request.DefaultSeverity;
        type.DefaultDueDays = request.DefaultDueDays;
        type.UpdatedAtUtc = DateTimeOffset.UtcNow;
        type.UpdatedBy = currentUser.ExternalSubject;
        type.UpdatedByUserId = currentUser.UserId;
        db.Update(type);
        await WriteAuditAsync("NoteTypeUpdated", nameof(NoteType), type.Id, new { type.NameAr, type.SortOrder, type.DefaultSeverity, type.DefaultDueDays }, old, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    public Task<NoteTypeDto> ActivateNoteTypeAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, request, true, "NoteTypeActivated", cancellationToken);

    public Task<NoteTypeDto> DeactivateNoteTypeAsync(Guid id, TransitionNoteRequest request, CancellationToken cancellationToken = default) =>
        SetActiveAsync(id, request, false, "NoteTypeDeactivated", cancellationToken);

    public async Task<IReadOnlyList<RoleNoteTypeGrantDto>> GetRoleGrantsAsync(Guid roleId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageRoleTypeAccess);
        _ = await RequireRoleAsync(roleId, cancellationToken);
        return await BuildRoleGrantDtosAsync(roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<RoleNoteTypeGrantDto>> ReplaceRoleGrantsAsync(Guid roleId, ReplaceRoleNoteTypeGrantsRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageRoleTypeAccess);
        _ = await RequireRoleAsync(roleId, cancellationToken);
        RequireReason(request.Reason);
        EnsureDistinctNoteTypeIds(request.Grants.Select(grant => grant.NoteTypeId));
        await EnsureNoteTypesExistAsync(request.Grants.Select(grant => grant.NoteTypeId), cancellationToken);
        await db.ExecuteInTransactionAsync(async ct =>
        {
            var now = DateTimeOffset.UtcNow;
            var existing = await db.RoleNoteTypeGrants.Where(g => g.RoleId == roleId).ToListAsync(ct);
            var requestedTypeIds = request.Grants.Select(grant => grant.NoteTypeId).ToHashSet();
            foreach (var grant in existing)
            {
                if (requestedTypeIds.Contains(grant.NoteTypeId))
                {
                    continue;
                }

                var before = CapabilitySnapshot(grant);
                grant.IsActive = false;
                grant.UpdatedAtUtc = now;
                grant.UpdatedBy = currentUser.ExternalSubject;
                grant.UpdatedByUserId = currentUser.UserId;
                db.Update(grant);
                if (before != CapabilitySnapshot(grant))
                {
                    AddAccessHistory(NoteTypeAccessPrincipalType.Role, roleId, grant.NoteTypeId, NoteTypeAccessChangeType.Revoked, before, CapabilitySnapshot(grant), request.Reason, now);
                }
            }

            foreach (var item in request.Grants)
            {
                var grant = existing.FirstOrDefault(g => g.NoteTypeId == item.NoteTypeId) ?? new RoleNoteTypeGrant { RoleId = roleId, NoteTypeId = item.NoteTypeId, CreatedBy = currentUser.ExternalSubject, CreatedByUserId = currentUser.UserId };
                var before = existing.Contains(grant) ? CapabilitySnapshot(grant) : null;
                ApplyGrant(item, grant);
                grant.IsActive = true;
                grant.UpdatedAtUtc = now;
                grant.UpdatedBy = currentUser.ExternalSubject;
                grant.UpdatedByUserId = currentUser.UserId;
                var after = CapabilitySnapshot(grant);
                if (before != after)
                {
                    AddAccessHistory(NoteTypeAccessPrincipalType.Role, roleId, grant.NoteTypeId, before is null ? NoteTypeAccessChangeType.Granted : NoteTypeAccessChangeType.Updated, before, after, request.Reason, now);
                }

                if (existing.Contains(grant))
                {
                    db.Update(grant);
                }
                else
                {
                    db.Add(grant);
                }
            }

            await WriteAuditAsync("RoleNoteTypeGrantsUpdated", nameof(RoleNoteTypeGrant), roleId, new { Count = request.Grants.Count }, null, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await BuildRoleGrantDtosAsync(roleId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserNoteTypeOverrideDto>> GetUserOverridesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageUserTypeOverrides);
        await RequireUserAsync(userId, cancellationToken);
        return await BuildUserOverrideDtosAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<UserNoteTypeOverrideDto>> ReplaceUserOverridesAsync(Guid userId, ReplaceUserNoteTypeOverridesRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageUserTypeOverrides);
        await RequireUserAsync(userId, cancellationToken);
        RequireReason(request.Reason);
        EnsureDistinctNoteTypeIds(request.Overrides.Select(overrideRow => overrideRow.NoteTypeId));
        await EnsureNoteTypesExistAsync(request.Overrides.Select(overrideRow => overrideRow.NoteTypeId), cancellationToken);

        await db.ExecuteInTransactionAsync(async ct =>
        {
            var now = DateTimeOffset.UtcNow;
            var existing = await db.UserNoteTypeOverrides.Where(o => o.UserId == userId).ToListAsync(ct);
            var requestedTypeIds = request.Overrides.Where(HasAnyOverride).Select(overrideRow => overrideRow.NoteTypeId).ToHashSet();
            foreach (var overrideRow in existing)
            {
                if (requestedTypeIds.Contains(overrideRow.NoteTypeId))
                {
                    continue;
                }

                var before = OverrideSnapshot(overrideRow);
                overrideRow.IsActive = false;
                overrideRow.UpdatedAtUtc = now;
                overrideRow.UpdatedBy = currentUser.ExternalSubject;
                overrideRow.UpdatedByUserId = currentUser.UserId;
                db.Update(overrideRow);
                if (before != OverrideSnapshot(overrideRow))
                {
                    AddAccessHistory(NoteTypeAccessPrincipalType.User, userId, overrideRow.NoteTypeId, NoteTypeAccessChangeType.OverrideRemoved, before, OverrideSnapshot(overrideRow), request.Reason, now);
                }
            }

            foreach (var item in request.Overrides)
            {
                if (!HasAnyOverride(item))
                {
                    continue;
                }

                var overrideRow = existing.FirstOrDefault(o => o.NoteTypeId == item.NoteTypeId) ?? new UserNoteTypeOverride { UserId = userId, NoteTypeId = item.NoteTypeId, CreatedBy = currentUser.ExternalSubject, CreatedByUserId = currentUser.UserId };
                var before = existing.Contains(overrideRow) ? OverrideSnapshot(overrideRow) : null;
                ApplyOverride(item, overrideRow, request.Reason.Trim());
                overrideRow.IsActive = true;
                overrideRow.UpdatedAtUtc = now;
                overrideRow.UpdatedBy = currentUser.ExternalSubject;
                overrideRow.UpdatedByUserId = currentUser.UserId;
                var after = OverrideSnapshot(overrideRow);
                if (before != after)
                {
                    AddAccessHistory(NoteTypeAccessPrincipalType.User, userId, overrideRow.NoteTypeId, ChangeTypeForOverride(item, before), before, after, request.Reason, now);
                }

                if (existing.Contains(overrideRow))
                {
                    db.Update(overrideRow);
                }
                else
                {
                    db.Add(overrideRow);
                }
            }

            await WriteAuditAsync("UserNoteTypeOverridesUpdated", nameof(UserNoteTypeOverride), userId, new { Count = request.Overrides.Count }, null, ct);
            await db.SaveChangesAsync(ct);
            return true;
        }, cancellationToken);

        return await BuildUserOverrideDtosAsync(userId, cancellationToken);
    }

    public Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageUserTypeOverrides);
        return access.GetEffectiveAccessAsync(userId, cancellationToken);
    }

    public async Task<UserNoteIntakeProfileDto> GetIntakeProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageIntakeProfiles);
        await RequireUserAsync(userId, cancellationToken);
        return await BuildIntakeProfileDtoAsync(userId, cancellationToken);
    }

    public async Task<UserNoteIntakeProfileDto> UpdateIntakeProfileAsync(Guid userId, UpdateUserNoteIntakeProfileRequest request, CancellationToken cancellationToken = default)
    {
        Ensure(PermissionCodes.NotesManageIntakeProfiles);
        await RequireUserAsync(userId, cancellationToken);
        RequireReason(request.Reason);
        await ValidateIntakeProfileAsync(userId, request.LockType, request.RegionId, request.FacilityId, cancellationToken);

        var profile = await db.UserNoteIntakeProfiles.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        if (profile is null)
        {
            profile = new UserNoteIntakeProfile { UserId = userId, CreatedBy = currentUser.ExternalSubject, CreatedByUserId = currentUser.UserId };
            db.Add(profile);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RowVersion))
            {
                throw new InvalidOperationException("إصدار السجل مطلوب.");
            }

            NoteAccessHelper.EnsureRowVersion(profile.RowVersion, request.RowVersion);
            db.Update(profile);
        }

        profile.LockType = request.LockType;
        profile.RegionId = request.LockType == NoteIntakeLockType.Facility ? null : request.RegionId;
        profile.FacilityId = request.FacilityId;
        profile.IsActive = true;
        profile.UpdatedAtUtc = DateTimeOffset.UtcNow;
        profile.UpdatedBy = currentUser.ExternalSubject;
        profile.UpdatedByUserId = currentUser.UserId;

        await WriteAuditAsync("UserNoteIntakeProfileUpdated", nameof(UserNoteIntakeProfile), userId, new { request.LockType, request.RegionId, request.FacilityId }, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return await BuildIntakeProfileDtoAsync(userId, cancellationToken);
    }

    public async Task<NoteIntakeContextDto> GetMyIntakeContextAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");
        var profile = await db.UserNoteIntakeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
        var regions = await orgScope.FilterRegions(db.Regions.AsNoTracking())
            .Where(r => r.IsActive)
            .OrderBy(r => r.NameAr)
            .Select(r => new NoteIntakeRegionDto(r.Id, r.NameAr))
            .ToListAsync(cancellationToken);
        var creatable = await access.GetAccessibleNoteTypesAsync(NoteTypeCapability.Create, cancellationToken);
        var lockedFacility = profile?.FacilityId is Guid facilityId
            ? await db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId, cancellationToken)
            : null;
        var lockedRegionId = profile?.LockType == NoteIntakeLockType.Facility ? lockedFacility?.RegionId : profile?.RegionId;
        var lockedRegion = lockedRegionId is Guid rid ? await db.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, cancellationToken) : null;

        return new NoteIntakeContextDto(
            profile?.LockType ?? NoteIntakeLockType.None,
            lockedRegionId,
            lockedRegion?.NameAr,
            profile?.FacilityId,
            lockedFacility?.NameAr,
            regions,
            creatable.Where(t => t.IsActive).ToList());
    }

    public async Task<IReadOnlyList<NoteIntakeFacilityDto>> GetMyIntakeFacilitiesAsync(Guid regionId, CancellationToken cancellationToken = default)
    {
        return await orgScope.FilterFacilities(db.Facilities.AsNoTracking())
            .Where(f => f.IsActive && f.RegionId == regionId)
            .OrderBy(f => f.NameAr)
            .Select(f => new NoteIntakeFacilityDto(f.Id, f.RegionId, f.NameAr))
            .ToListAsync(cancellationToken);
    }

    private async Task<NoteTypeDto> SetActiveAsync(Guid id, TransitionNoteRequest request, bool isActive, string action, CancellationToken cancellationToken)
    {
        Ensure(PermissionCodes.NotesManageTypes);
        var type = await db.NoteTypes.FirstOrDefaultAsync(t => t.Id == id, cancellationToken) ?? throw new KeyNotFoundException(NoteTypeNotFoundMessage);
        NoteAccessHelper.EnsureRowVersion(type.RowVersion, request.RowVersion);
        type.IsActive = isActive;
        type.UpdatedAtUtc = DateTimeOffset.UtcNow;
        type.UpdatedBy = currentUser.ExternalSubject;
        type.UpdatedByUserId = currentUser.UserId;
        db.Update(type);
        await WriteAuditAsync(action, nameof(NoteType), type.Id, new { type.Code, type.IsActive }, null, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(type);
    }

    private async Task<IReadOnlyList<RoleNoteTypeGrantDto>> BuildRoleGrantDtosAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var types = await db.NoteTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.NameAr).ToListAsync(cancellationToken);
        var grants = await db.RoleNoteTypeGrants.AsNoTracking().Where(g => g.RoleId == roleId && g.IsActive).ToListAsync(cancellationToken);
        return types.Select(type =>
        {
            var grant = grants.FirstOrDefault(g => g.NoteTypeId == type.Id);
            return new RoleNoteTypeGrantDto(type.Id, type.Code, type.NameAr, ToCapabilityDto(grant), grant is null ? null : Convert.ToBase64String(grant.RowVersion));
        }).ToList();
    }

    private async Task<IReadOnlyList<UserNoteTypeOverrideDto>> BuildUserOverrideDtosAsync(Guid userId, CancellationToken cancellationToken)
    {
        var types = await db.NoteTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.NameAr).ToListAsync(cancellationToken);
        var overrides = await db.UserNoteTypeOverrides.AsNoTracking().Where(o => o.UserId == userId && o.IsActive).ToListAsync(cancellationToken);
        return types.Select(type =>
        {
            var item = overrides.FirstOrDefault(o => o.NoteTypeId == type.Id);
            return new UserNoteTypeOverrideDto(
                type.Id, type.Code, type.NameAr,
                item?.CanViewOverride, item?.CanCreateOverride, item?.CanAssignOverride, item?.CanProcessOverride,
                item?.CanSubmitForVerificationOverride, item?.CanReviewOverride, item?.CanCancelOverride,
                item?.CanReopenOverride, item?.CanArchiveOverride, item?.CanRestoreOverride,
                item?.Reason, item is null ? null : Convert.ToBase64String(item.RowVersion));
        }).ToList();
    }

    private async Task<UserNoteIntakeProfileDto> BuildIntakeProfileDtoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var profile = await db.UserNoteIntakeProfiles.AsNoTracking().FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
        if (profile is null)
        {
            return new UserNoteIntakeProfileDto(null, userId, NoteIntakeLockType.None, null, null, null, null, true, null, null);
        }

        var facility = profile.FacilityId is Guid fid ? await db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid, cancellationToken) : null;
        var regionId = profile.LockType == NoteIntakeLockType.Facility ? facility?.RegionId : profile.RegionId;
        var region = regionId is Guid rid ? await db.Regions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == rid, cancellationToken) : null;
        return new UserNoteIntakeProfileDto(profile.Id, userId, profile.LockType, regionId, region?.NameAr, profile.FacilityId, facility?.NameAr, true, null, Convert.ToBase64String(profile.RowVersion));
    }

    private async Task ValidateIntakeProfileAsync(Guid userId, NoteIntakeLockType lockType, Guid? regionId, Guid? facilityId, CancellationToken cancellationToken)
    {
        ValidateIntakeLockShape(lockType, regionId, facilityId);

        if (regionId.HasValue)
        {
            await EnsureRegionLockInScopeAsync(userId, regionId.Value, cancellationToken);
        }

        if (facilityId.HasValue)
        {
            await EnsureFacilityLockInScopeAsync(userId, facilityId.Value, cancellationToken);
        }
    }

    private static void ValidateIntakeLockShape(NoteIntakeLockType lockType, Guid? regionId, Guid? facilityId)
    {
        switch (lockType)
        {
            case NoteIntakeLockType.None when regionId.HasValue || facilityId.HasValue:
                throw new InvalidOperationException("لا يقبل سياق الإدخال دون تثبيت معرفات منطقة أو موقع.");
            case NoteIntakeLockType.Region when !regionId.HasValue || facilityId.HasValue:
                throw new InvalidOperationException("تثبيت المنطقة يتطلب RegionId فقط.");
            case NoteIntakeLockType.Facility when !facilityId.HasValue:
                throw new InvalidOperationException("تثبيت الموقع يتطلب FacilityId.");
        }
    }

    private async Task EnsureRegionLockInScopeAsync(Guid userId, Guid regionId, CancellationToken cancellationToken)
    {
        var inScope = await db.UserScopes.AnyAsync(
            scope => scope.UserId == userId && scope.IsActive &&
                (scope.ScopeType == ScopeType.Global || scope.ScopeType == ScopeType.Region && scope.RegionId == regionId),
            cancellationToken);
        if (!inScope)
        {
            throw new UnauthorizedAccessException("المنطقة المثبتة خارج نطاق المستخدم.");
        }
    }

    private async Task EnsureFacilityLockInScopeAsync(Guid userId, Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == facilityId && f.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("الموقع غير موجود.");
        var inScope = await db.UserScopes.AnyAsync(s => s.UserId == userId && s.IsActive &&
                (s.ScopeType == ScopeType.Global ||
                 s.ScopeType == ScopeType.Region && s.RegionId == facility.RegionId ||
                 s.ScopeType == ScopeType.Facility && s.FacilityId == facility.Id),
            cancellationToken);
        if (!inScope)
        {
            throw new UnauthorizedAccessException("الموقع المثبت خارج نطاق المستخدم.");
        }
    }

    private async Task<Role> RequireRoleAsync(Guid roleId, CancellationToken cancellationToken) =>
        await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken) ?? throw new KeyNotFoundException("الدور غير موجود.");

    private async Task RequireUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            throw new KeyNotFoundException("المستخدم غير موجود.");
        }
    }

    private async Task EnsureNoteTypeExistsAsync(Guid noteTypeId, CancellationToken cancellationToken)
    {
        if (!await db.NoteTypes.AnyAsync(t => t.Id == noteTypeId, cancellationToken))
        {
            throw new KeyNotFoundException(NoteTypeNotFoundMessage);
        }
    }

    private async Task EnsureNoteTypesExistAsync(IEnumerable<Guid> noteTypeIds, CancellationToken cancellationToken)
    {
        var ids = noteTypeIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var existing = await db.NoteTypes.AsNoTracking().Where(type => ids.Contains(type.Id)).Select(type => type.Id).ToListAsync(cancellationToken);
        if (existing.Count != ids.Count)
        {
            throw new KeyNotFoundException(NoteTypeNotFoundMessage);
        }
    }

    private static void EnsureDistinctNoteTypeIds(IEnumerable<Guid> noteTypeIds)
    {
        var ids = noteTypeIds.ToList();
        if (ids.Count != ids.Distinct().Count())
        {
            throw new InvalidOperationException("لا يمكن تكرار نوع الملاحظة في الطلب.");
        }
    }

    private void Ensure(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException("ليست لديك الصلاحية المطلوبة.");
        }
    }

    private void EnsureAny(params string[] permissions)
    {
        if (!permissions.Any(currentUser.HasPermission))
        {
            throw new UnauthorizedAccessException("ليست لديك الصلاحية المطلوبة.");
        }
    }

    private Task WriteAuditAsync(string action, string entityType, Guid entityId, object? values, object? oldValues, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditEntry
        {
            Action = action,
            Module = NoteAccessHelper.ModuleName,
            EntityType = entityType,
            EntityId = entityId.ToString(),
            OldValues = oldValues,
            NewValues = values
        }, cancellationToken);

    private static NoteTypeDto ToDto(NoteType type) => new(
        type.Id, type.Code, type.NameAr, type.DescriptionAr, type.EntryInstructionsAr, type.SortOrder,
        type.IsActive, type.DefaultSeverity, NoteDisplay.SeverityAr(type.DefaultSeverity), type.DefaultDueDays,
        Convert.ToBase64String(type.RowVersion));

    private static NoteTypeCapabilityDto ToCapabilityDto(RoleNoteTypeGrant? grant) => new(
        grant?.CanView == true,
        grant?.CanCreate == true,
        grant?.CanAssign == true,
        grant?.CanProcess == true,
        grant?.CanSubmitForVerification == true,
        grant?.CanReview == true,
        grant?.CanCancel == true,
        grant?.CanReopen == true,
        grant?.CanArchive == true,
        grant?.CanRestore == true);

    private static void ApplyGrant(ReplaceRoleNoteTypeGrantItem item, RoleNoteTypeGrant grant)
    {
        grant.CanView = item.CanView;
        grant.CanCreate = item.CanCreate;
        grant.CanAssign = item.CanAssign;
        grant.CanProcess = item.CanProcess;
        grant.CanSubmitForVerification = item.CanSubmitForVerification;
        grant.CanReview = item.CanReview;
        grant.CanCancel = item.CanCancel;
        grant.CanReopen = item.CanReopen;
        grant.CanArchive = item.CanArchive;
        grant.CanRestore = item.CanRestore;
    }

    private static void ApplyOverride(ReplaceUserNoteTypeOverrideItem item, UserNoteTypeOverride overrideRow, string reason)
    {
        overrideRow.CanViewOverride = item.CanViewOverride;
        overrideRow.CanCreateOverride = item.CanCreateOverride;
        overrideRow.CanAssignOverride = item.CanAssignOverride;
        overrideRow.CanProcessOverride = item.CanProcessOverride;
        overrideRow.CanSubmitForVerificationOverride = item.CanSubmitForVerificationOverride;
        overrideRow.CanReviewOverride = item.CanReviewOverride;
        overrideRow.CanCancelOverride = item.CanCancelOverride;
        overrideRow.CanReopenOverride = item.CanReopenOverride;
        overrideRow.CanArchiveOverride = item.CanArchiveOverride;
        overrideRow.CanRestoreOverride = item.CanRestoreOverride;
        overrideRow.Reason = reason;
    }

    private static bool HasAnyOverride(ReplaceUserNoteTypeOverrideItem item) =>
        item.CanViewOverride.HasValue || item.CanCreateOverride.HasValue || item.CanAssignOverride.HasValue ||
        item.CanProcessOverride.HasValue || item.CanSubmitForVerificationOverride.HasValue ||
        item.CanReviewOverride.HasValue || item.CanCancelOverride.HasValue || item.CanReopenOverride.HasValue ||
        item.CanArchiveOverride.HasValue || item.CanRestoreOverride.HasValue;

    private void AddAccessHistory(
        NoteTypeAccessPrincipalType principalType,
        Guid principalId,
        Guid noteTypeId,
        NoteTypeAccessChangeType changeType,
        string? before,
        string? after,
        string reason,
        DateTimeOffset now) =>
        db.Add(new NoteTypeAccessChangeHistory
        {
            PrincipalType = principalType,
            PrincipalId = principalId,
            NoteTypeId = noteTypeId,
            ChangeType = changeType,
            PreviousCapabilitiesJson = before,
            NewCapabilitiesJson = after,
            ChangedAtUtc = now,
            ChangedByUserId = currentUser.UserId,
            Reason = reason.Trim(),
            CorrelationId = currentUser.CorrelationId
        });

    private static string CapabilitySnapshot(RoleNoteTypeGrant grant) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            grant.IsActive,
            grant.CanView,
            grant.CanCreate,
            grant.CanAssign,
            grant.CanProcess,
            grant.CanSubmitForVerification,
            grant.CanReview,
            grant.CanCancel,
            grant.CanReopen,
            grant.CanArchive,
            grant.CanRestore
        });

    private static string OverrideSnapshot(UserNoteTypeOverride overrideRow) =>
        System.Text.Json.JsonSerializer.Serialize(new
        {
            overrideRow.IsActive,
            overrideRow.CanViewOverride,
            overrideRow.CanCreateOverride,
            overrideRow.CanAssignOverride,
            overrideRow.CanProcessOverride,
            overrideRow.CanSubmitForVerificationOverride,
            overrideRow.CanReviewOverride,
            overrideRow.CanCancelOverride,
            overrideRow.CanReopenOverride,
            overrideRow.CanArchiveOverride,
            overrideRow.CanRestoreOverride
        });

    private static NoteTypeAccessChangeType ChangeTypeForOverride(
        ReplaceUserNoteTypeOverrideItem item,
        string? before)
    {
        if (item.CanViewOverride == false ||
            item.CanCreateOverride == false ||
            item.CanAssignOverride == false ||
            item.CanProcessOverride == false ||
            item.CanSubmitForVerificationOverride == false ||
            item.CanReviewOverride == false ||
            item.CanCancelOverride == false ||
            item.CanReopenOverride == false ||
            item.CanArchiveOverride == false ||
            item.CanRestoreOverride == false)
        {
            return NoteTypeAccessChangeType.DirectDenyAdded;
        }

        return before is null
            ? NoteTypeAccessChangeType.DirectAllowAdded
            : NoteTypeAccessChangeType.Updated;
    }

    private static void RequireReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("السبب مطلوب.");
        }
    }

    private static string? TrimOrNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
