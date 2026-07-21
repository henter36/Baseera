namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Pure assignee-scope ∩ note-scope checks used by assignment validation.
/// Kept separate from the command service so each branch stays under cognitive complexity limits.
/// </summary>
public static class NoteAssigneeScopeIntersection
{
    public static Task<bool> IntersectsAsync(
        IBaseeraDbContext db,
        IReadOnlyList<UserScopeSnapshot> scopes,
        OperationalNote note,
        CancellationToken cancellationToken) =>
        note.ScopeType switch
        {
            ScopeType.Region => IntersectsRegionAsync(db, scopes, note.RegionId, cancellationToken),
            ScopeType.Facility => IntersectsFacilityAsync(db, scopes, note.FacilityId, cancellationToken),
            ScopeType.FacilityUnit => IntersectsFacilityUnitAsync(
                db, scopes, note.FacilityId, note.FacilityUnitId, cancellationToken),
            ScopeType.Global => Task.FromResult(HasGlobalScope(scopes)),
            ScopeType.Headquarters => Task.FromResult(HasHeadquartersScope(scopes)),
            _ => Task.FromResult(false)
        };

    public static async Task<bool> IntersectsRegionAsync(
        IBaseeraDbContext db,
        IReadOnlyList<UserScopeSnapshot> scopes,
        Guid? regionId,
        CancellationToken cancellationToken)
    {
        if (regionId is not Guid rid)
        {
            return false;
        }

        if (scopes.Any(s => HasRegionAccess(s, rid)))
        {
            return true;
        }

        var facilityIds = CollectFacilityIds(scopes);
        if (facilityIds.Count == 0)
        {
            return false;
        }

        var facilityRegionMap = await db.Facilities
            .Where(f => facilityIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.RegionId, cancellationToken);

        return scopes.Any(s =>
            s.FacilityId is Guid fid &&
            facilityRegionMap.TryGetValue(fid, out var mappedRegionId) &&
            mappedRegionId == rid);
    }

    public static async Task<bool> IntersectsFacilityAsync(
        IBaseeraDbContext db,
        IReadOnlyList<UserScopeSnapshot> scopes,
        Guid? facilityId,
        CancellationToken cancellationToken)
    {
        if (facilityId is not Guid fid)
        {
            return false;
        }

        if (scopes.Any(s => HasFacilityAccess(s, fid)))
        {
            return true;
        }

        var noteFacilityRegionId = await GetFacilityRegionIdAsync(db, fid, cancellationToken);
        return noteFacilityRegionId is Guid regionId && scopes.Any(s => HasRegionAccess(s, regionId));
    }

    public static async Task<bool> IntersectsFacilityUnitAsync(
        IBaseeraDbContext db,
        IReadOnlyList<UserScopeSnapshot> scopes,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken)
    {
        if (facilityUnitId is not Guid uid)
        {
            return false;
        }

        if (scopes.Any(s => s.ScopeType == ScopeType.FacilityUnit && s.FacilityUnitId == uid))
        {
            return true;
        }

        if (facilityId is not Guid fid)
        {
            return false;
        }

        if (scopes.Any(s => HasFacilityOrMultiFacilityAccess(s, fid)))
        {
            return true;
        }

        var noteFacilityRegionId = await GetFacilityRegionIdAsync(db, fid, cancellationToken);
        return noteFacilityRegionId is Guid regionId && scopes.Any(s => HasRegionAccess(s, regionId));
    }

    public static bool HasGlobalScope(IReadOnlyList<UserScopeSnapshot> scopes) =>
        scopes.Any(s => s.ScopeType == ScopeType.Global);

    public static bool HasHeadquartersScope(IReadOnlyList<UserScopeSnapshot> scopes) =>
        scopes.Any(s => s.ScopeType is ScopeType.Headquarters or ScopeType.Global);

    public static async Task<Guid?> GetFacilityRegionIdAsync(
        IBaseeraDbContext db,
        Guid facilityId,
        CancellationToken cancellationToken) =>
        await db.Facilities
            .Where(f => f.Id == facilityId)
            .Select(f => (Guid?)f.RegionId)
            .FirstOrDefaultAsync(cancellationToken);

    private static bool HasRegionAccess(UserScopeSnapshot scope, Guid regionId) =>
        (scope.ScopeType is ScopeType.Region or ScopeType.MultipleRegions) &&
        scope.RegionId == regionId;

    private static bool HasFacilityAccess(UserScopeSnapshot scope, Guid facilityId) =>
        (scope.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities or ScopeType.FacilityUnit) &&
        scope.FacilityId == facilityId;

    private static bool HasFacilityOrMultiFacilityAccess(UserScopeSnapshot scope, Guid facilityId) =>
        (scope.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities) &&
        scope.FacilityId == facilityId;

    private static HashSet<Guid> CollectFacilityIds(IReadOnlyList<UserScopeSnapshot> scopes)
    {
        var ids = new HashSet<Guid>();
        foreach (var scope in scopes)
        {
            if (scope.FacilityId is Guid fid)
            {
                ids.Add(fid);
            }
        }

        return ids;
    }

    public static bool IntersectsUserScopeForRouting(UserScope scope, OperationalNote note) =>
        scope.ScopeType switch
        {
            ScopeType.Global => true,
            ScopeType.Headquarters => note.ScopeType == ScopeType.Headquarters,
            ScopeType.Region or ScopeType.MultipleRegions =>
                note.RegionId == scope.RegionId,
            ScopeType.Facility or ScopeType.MultipleFacilities =>
                note.FacilityId == scope.FacilityId,
            ScopeType.FacilityUnit =>
                note.FacilityUnitId == scope.FacilityUnitId,
            _ => false
        };

    public static bool IntersectsAnyUserScopeForRouting(IEnumerable<UserScope> scopes, OperationalNote note) =>
        scopes.Any(scope => IntersectsUserScopeForRouting(scope, note));
}
