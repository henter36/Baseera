namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public enum NoteTypeCapability
{
    View,
    Create,
    Assign,
    Process,
    SubmitForVerification,
    Review,
    Cancel,
    Reopen,
    Archive,
    Restore
}

public interface INoteTypeAccessService
{
    Task<bool> CanViewAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanCreateAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanAssignAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanProcessAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanSubmitForVerificationAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanReviewAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanCancelAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanReopenAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanArchiveAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<bool> CanRestoreAsync(Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<EffectiveNoteTypeAccessDto?> GetEffectiveAccessAsync(Guid userId, Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>> GetEffectiveAccessForUsersAsync(IEnumerable<Guid> userIds, Guid noteTypeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<NoteTypeDto>> GetAccessibleNoteTypesAsync(NoteTypeCapability capability, CancellationToken cancellationToken = default);
    Task<IQueryable<OperationalNote>> FilterViewableNotesAsync(IQueryable<OperationalNote> query, CancellationToken cancellationToken = default);
    Task EnsureCanAsync(Guid noteTypeId, NoteTypeCapability capability, CancellationToken cancellationToken = default);
}

public sealed class NoteTypeAccessService(IBaseeraDbContext db, ICurrentUser currentUser) : INoteTypeAccessService
{
    public Task<bool> CanViewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.View, cancellationToken);

    public Task<bool> CanCreateAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Create, cancellationToken);

    public Task<bool> CanAssignAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Assign, cancellationToken);

    public Task<bool> CanProcessAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Process, cancellationToken);

    public Task<bool> CanSubmitForVerificationAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.SubmitForVerification, cancellationToken);

    public Task<bool> CanReviewAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Review, cancellationToken);

    public Task<bool> CanCancelAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Cancel, cancellationToken);

    public Task<bool> CanReopenAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Reopen, cancellationToken);

    public Task<bool> CanArchiveAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Archive, cancellationToken);

    public Task<bool> CanRestoreAsync(Guid noteTypeId, CancellationToken cancellationToken = default) =>
        CanAsync(noteTypeId, NoteTypeCapability.Restore, cancellationToken);

    public async Task EnsureCanAsync(Guid noteTypeId, NoteTypeCapability capability, CancellationToken cancellationToken = default)
    {
        if (!await CanAsync(noteTypeId, capability, cancellationToken))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية نوع الملاحظة المطلوبة لهذه العملية.");
        }
    }

    public async Task<IQueryable<OperationalNote>> FilterViewableNotesAsync(IQueryable<OperationalNote> query, CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var noteTypeIds = await GetAccessibleNoteTypeIdsAsync(userId, NoteTypeCapability.View, cancellationToken);
        return query.Where(note => noteTypeIds.Contains(note.NoteTypeId));
    }

    public async Task<IReadOnlyList<NoteTypeDto>> GetAccessibleNoteTypesAsync(NoteTypeCapability capability, CancellationToken cancellationToken = default)
    {
        var userId = RequireCurrentUserId();
        var ids = await GetAccessibleNoteTypeIdsAsync(userId, capability, cancellationToken);
        var noteTypes = await db.NoteTypes
            .AsNoTracking()
            .Where(type => ids.Contains(type.Id))
            .OrderBy(type => type.SortOrder)
            .ThenBy(type => type.NameAr)
            .ToListAsync(cancellationToken);

        return noteTypes.Select(ToDto).ToList();
    }

    public async Task<EffectiveNoteTypeAccessDto?> GetEffectiveAccessAsync(Guid userId, Guid noteTypeId, CancellationToken cancellationToken = default)
    {
        var all = await GetEffectiveAccessAsync(userId, cancellationToken);
        return all.FirstOrDefault(item => item.NoteTypeId == noteTypeId);
    }

    public async Task<IReadOnlyDictionary<Guid, EffectiveNoteTypeAccessDto?>> GetEffectiveAccessForUsersAsync(
        IEnumerable<Guid> userIds,
        Guid noteTypeId,
        CancellationToken cancellationToken = default)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, EffectiveNoteTypeAccessDto?>();
        }

        var noteType = await db.NoteTypes.AsNoTracking().FirstOrDefaultAsync(type => type.Id == noteTypeId, cancellationToken);
        if (noteType is null)
        {
            return ids.ToDictionary(id => id, _ => (EffectiveNoteTypeAccessDto?)null);
        }

        var userRoles = await db.UserRoles
            .AsNoTracking()
            .Where(role => ids.Contains(role.UserId))
            .Select(role => new { role.UserId, role.RoleId })
            .ToListAsync(cancellationToken);
        var roleIds = userRoles.Select(role => role.RoleId).Distinct().ToList();
        var grants = await db.RoleNoteTypeGrants
            .AsNoTracking()
            .Where(grant => grant.IsActive && grant.NoteTypeId == noteTypeId && roleIds.Contains(grant.RoleId))
            .ToListAsync(cancellationToken);
        var overrides = await db.UserNoteTypeOverrides
            .AsNoTracking()
            .Where(overrideRow => overrideRow.IsActive && overrideRow.NoteTypeId == noteTypeId && ids.Contains(overrideRow.UserId))
            .ToListAsync(cancellationToken);

        var rolesByUser = userRoles.ToLookup(role => role.UserId, role => role.RoleId);
        var grantsByRole = grants.ToLookup(grant => grant.RoleId);
        var overrideByUser = overrides.ToDictionary(overrideRow => overrideRow.UserId);
        return ids.ToDictionary(
            id => id,
            id =>
            {
                var userGrants = rolesByUser[id].SelectMany(roleId => grantsByRole[roleId]);
                overrideByUser.TryGetValue(id, out var overrideRow);
                return (EffectiveNoteTypeAccessDto?)BuildAccessDto(noteType, userGrants, overrideRow);
            });
    }

    public async Task<IReadOnlyList<EffectiveNoteTypeAccessDto>> GetEffectiveAccessAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var noteTypes = await db.NoteTypes.AsNoTracking().OrderBy(t => t.SortOrder).ThenBy(t => t.NameAr).ToListAsync(cancellationToken);
        var roleIds = await db.UserRoles.Where(role => role.UserId == userId).Select(role => role.RoleId).ToListAsync(cancellationToken);
        var grants = await db.RoleNoteTypeGrants.AsNoTracking().Where(grant => grant.IsActive && roleIds.Contains(grant.RoleId)).ToListAsync(cancellationToken);
        var overrides = await db.UserNoteTypeOverrides.AsNoTracking().Where(overrideRow => overrideRow.IsActive && overrideRow.UserId == userId).ToListAsync(cancellationToken);

        return noteTypes
            .Select(noteType => BuildAccessDto(noteType, grants.Where(g => g.NoteTypeId == noteType.Id), overrides.FirstOrDefault(o => o.NoteTypeId == noteType.Id)))
            .ToList();
    }

    private async Task<bool> CanAsync(Guid noteTypeId, NoteTypeCapability capability, CancellationToken cancellationToken)
    {
        var userId = RequireCurrentUserId();
        var access = await GetEffectiveAccessAsync(userId, noteTypeId, cancellationToken);
        return access is not null && Pick(access, capability).Allowed;
    }

    private async Task<List<Guid>> GetAccessibleNoteTypeIdsAsync(Guid userId, NoteTypeCapability capability, CancellationToken cancellationToken)
    {
        var all = await GetEffectiveAccessAsync(userId, cancellationToken);
        return all.Where(item => Pick(item, capability).Allowed).Select(item => item.NoteTypeId).ToList();
    }

    private Guid RequireCurrentUserId() =>
        currentUser.UserId ?? throw new UnauthorizedAccessException("المستخدم غير مصادق.");

    private static EffectiveNoteTypeAccessDto BuildAccessDto(
        NoteType noteType,
        IEnumerable<RoleNoteTypeGrant> grants,
        UserNoteTypeOverride? overrideRow)
    {
        return new EffectiveNoteTypeAccessDto(
            noteType.Id,
            noteType.Code,
            noteType.NameAr,
            noteType.IsActive,
            Decide(grants, overrideRow, NoteTypeCapability.View),
            Decide(grants, overrideRow, NoteTypeCapability.Create),
            Decide(grants, overrideRow, NoteTypeCapability.Assign),
            Decide(grants, overrideRow, NoteTypeCapability.Process),
            Decide(grants, overrideRow, NoteTypeCapability.SubmitForVerification),
            Decide(grants, overrideRow, NoteTypeCapability.Review),
            Decide(grants, overrideRow, NoteTypeCapability.Cancel),
            Decide(grants, overrideRow, NoteTypeCapability.Reopen),
            Decide(grants, overrideRow, NoteTypeCapability.Archive),
            Decide(grants, overrideRow, NoteTypeCapability.Restore));
    }

    private static NoteTypeCapabilityDecisionDto Decide(
        IEnumerable<RoleNoteTypeGrant> grants,
        UserNoteTypeOverride? overrideRow,
        NoteTypeCapability capability)
    {
        var roleAllowed = grants.Any(grant => CapabilityValue(grant, capability));
        var overrideValue = OverrideValue(overrideRow, capability);
        return overrideValue switch
        {
            true => new NoteTypeCapabilityDecisionDto(true, "Direct Allow"),
            false => new NoteTypeCapabilityDecisionDto(false, "Direct Deny"),
            null when roleAllowed => new NoteTypeCapabilityDecisionDto(true, "Role"),
            _ => new NoteTypeCapabilityDecisionDto(false, "No Grant")
        };
    }

    private static NoteTypeCapabilityDecisionDto Pick(EffectiveNoteTypeAccessDto access, NoteTypeCapability capability) => capability switch
    {
        NoteTypeCapability.View => access.View,
        NoteTypeCapability.Create => access.Create,
        NoteTypeCapability.Assign => access.Assign,
        NoteTypeCapability.Process => access.Process,
        NoteTypeCapability.SubmitForVerification => access.SubmitForVerification,
        NoteTypeCapability.Review => access.Review,
        NoteTypeCapability.Cancel => access.Cancel,
        NoteTypeCapability.Reopen => access.Reopen,
        NoteTypeCapability.Archive => access.Archive,
        NoteTypeCapability.Restore => access.Restore,
        _ => access.View
    };

    private static bool CapabilityValue(RoleNoteTypeGrant grant, NoteTypeCapability capability) => capability switch
    {
        NoteTypeCapability.View => grant.CanView,
        NoteTypeCapability.Create => grant.CanCreate,
        NoteTypeCapability.Assign => grant.CanAssign,
        NoteTypeCapability.Process => grant.CanProcess,
        NoteTypeCapability.SubmitForVerification => grant.CanSubmitForVerification,
        NoteTypeCapability.Review => grant.CanReview,
        NoteTypeCapability.Cancel => grant.CanCancel,
        NoteTypeCapability.Reopen => grant.CanReopen,
        NoteTypeCapability.Archive => grant.CanArchive,
        NoteTypeCapability.Restore => grant.CanRestore,
        _ => false
    };

    private static bool? OverrideValue(UserNoteTypeOverride? overrideRow, NoteTypeCapability capability) => capability switch
    {
        NoteTypeCapability.View => overrideRow?.CanViewOverride,
        NoteTypeCapability.Create => overrideRow?.CanCreateOverride,
        NoteTypeCapability.Assign => overrideRow?.CanAssignOverride,
        NoteTypeCapability.Process => overrideRow?.CanProcessOverride,
        NoteTypeCapability.SubmitForVerification => overrideRow?.CanSubmitForVerificationOverride,
        NoteTypeCapability.Review => overrideRow?.CanReviewOverride,
        NoteTypeCapability.Cancel => overrideRow?.CanCancelOverride,
        NoteTypeCapability.Reopen => overrideRow?.CanReopenOverride,
        NoteTypeCapability.Archive => overrideRow?.CanArchiveOverride,
        NoteTypeCapability.Restore => overrideRow?.CanRestoreOverride,
        _ => null
    };

    private static NoteTypeDto ToDto(NoteType type) => new(
        type.Id,
        type.Code,
        type.NameAr,
        type.DescriptionAr,
        type.EntryInstructionsAr,
        type.SortOrder,
        type.IsActive,
        type.DefaultSeverity,
        NoteDisplay.SeverityAr(type.DefaultSeverity),
        type.DefaultDueDays,
        Convert.ToBase64String(type.RowVersion));
}
