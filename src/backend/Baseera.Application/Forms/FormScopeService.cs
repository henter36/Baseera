namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormScopeService
{
    bool CanAccess(FormDefinition form);
    IQueryable<FormDefinition> FilterQueryable(IQueryable<FormDefinition> query);
    Task<IQueryable<FormDefinition>> FilterQueryableAsync(IQueryable<FormDefinition> query, CancellationToken cancellationToken = default);
    void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId);
    Task EnsureOrgEntitiesActiveAsync(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId, CancellationToken cancellationToken = default);
}

public sealed class FormScopeService(
    IOrganizationalScopeService orgScope,
    ICurrentUser currentUser,
    IBaseeraDbContext db) : IFormScopeService
{
    private static readonly ScopeType[] Supported =
    [
        ScopeType.Global,
        ScopeType.Headquarters,
        ScopeType.Region,
        ScopeType.Facility,
        ScopeType.FacilityUnit
    ];

    public bool CanAccess(FormDefinition form) => orgScope.CanAccess(form);

    public IQueryable<FormDefinition> FilterQueryable(IQueryable<FormDefinition> query)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterWithScopeIds(query, BuildAccessibleScopeIds());
    }

    public async Task<IQueryable<FormDefinition>> FilterQueryableAsync(
        IQueryable<FormDefinition> query,
        CancellationToken cancellationToken = default)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterWithScopeIds(query, await BuildAccessibleScopeIdsAsync(cancellationToken));
    }

    public void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!Supported.Contains(scopeType))
        {
            throw new InvalidOperationException("نطاق النموذج غير مدعوم في هذه المرحلة.");
        }

        switch (scopeType)
        {
            case ScopeType.Global:
            case ScopeType.Headquarters:
                EnsureNoIds(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Region:
                EnsureRegionOnly(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Facility:
                EnsureFacilityShape(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.FacilityUnit:
                EnsureUnitShape(regionId, facilityId, facilityUnitId);
                break;
        }
    }

    public async Task EnsureOrgEntitiesActiveAsync(
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken = default)
    {
        if (scopeType == ScopeType.Region && regionId.HasValue)
        {
            await EnsureRegionExistsAsync(regionId.Value, cancellationToken);
        }

        if (facilityId.HasValue)
        {
            await EnsureFacilityConsistentAsync(regionId, facilityId.Value, cancellationToken);
        }

        if (facilityUnitId.HasValue)
        {
            if (!facilityId.HasValue)
            {
                throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
            }

            await EnsureUnitBelongsToFacilityAsync(facilityId.Value, facilityUnitId.Value, cancellationToken);
        }
    }

    private IQueryable<FormDefinition> FilterWithScopeIds(
        IQueryable<FormDefinition> query,
        (HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds) ids)
    {
        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var (regionIds, fullFacilityIds, unitIds) = ids;
        var hasHq = orgScope.HasHeadquartersAccess;

        return query.Where(f =>
            (f.ScopeType == ScopeType.Headquarters && hasHq) ||
            (f.ScopeType == ScopeType.Region && f.RegionId.HasValue && regionIds.Contains(f.RegionId.Value)) ||
            (f.ScopeType == ScopeType.Facility && f.FacilityId.HasValue && fullFacilityIds.Contains(f.FacilityId.Value)) ||
            (f.ScopeType == ScopeType.FacilityUnit && (
                (f.FacilityId.HasValue && fullFacilityIds.Contains(f.FacilityId.Value)) ||
                (f.FacilityUnitId.HasValue && unitIds.Contains(f.FacilityUnitId.Value)))));
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds) BuildAccessibleScopeIds()
    {
        var ids = CollectScopeIdsFromUser();
        ExpandFacilitiesFromAccessibleRegions(ids.RegionIds, ids.FullFacilityIds);
        ExpandRegionsFromAccessibleFacilities(ids.RegionIds, ids.FullFacilityIds, ids.UnitIds);
        return ids;
    }

    private async Task<(HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds)> BuildAccessibleScopeIdsAsync(
        CancellationToken cancellationToken)
    {
        var ids = CollectScopeIdsFromUser();
        await ExpandFacilitiesFromAccessibleRegionsAsync(ids.RegionIds, ids.FullFacilityIds, cancellationToken);
        await ExpandRegionsFromAccessibleFacilitiesAsync(ids.RegionIds, ids.FullFacilityIds, ids.UnitIds, cancellationToken);
        return ids;
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds) CollectScopeIdsFromUser()
    {
        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue &&
                        (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        var fullFacilityIds = currentUser.Scopes
            .Where(s => s.FacilityId.HasValue &&
                        (s.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities))
            .Select(s => s.FacilityId!.Value)
            .ToHashSet();

        var unitIds = currentUser.Scopes
            .Where(s => s.FacilityUnitId.HasValue && s.ScopeType == ScopeType.FacilityUnit)
            .Select(s => s.FacilityUnitId!.Value)
            .ToHashSet();

        return (regionIds, fullFacilityIds, unitIds);
    }

    /// <summary>
    /// A region-scoped user can see every facility within their regions, so add those facilities
    /// to the "full facility access" set (grants Facility-scoped AND FacilityUnit-scoped form visibility).
    /// </summary>
    private void ExpandFacilitiesFromAccessibleRegions(HashSet<Guid> regionIds, HashSet<Guid> fullFacilityIds)
    {
        foreach (var id in db.Facilities
                     .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
                     .Select(f => f.Id)
                     .ToList())
        {
            fullFacilityIds.Add(id);
        }
    }

    private async Task ExpandFacilitiesFromAccessibleRegionsAsync(
        HashSet<Guid> regionIds,
        HashSet<Guid> fullFacilityIds,
        CancellationToken cancellationToken)
    {
        var regionFacilityIds = await db.Facilities
            .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in regionFacilityIds)
        {
            fullFacilityIds.Add(id);
        }
    }

    /// <summary>
    /// A user who only has a facility-unit scope should still see Region-scoped forms for the
    /// region that owns their facility, even though they don't have full facility access.
    /// </summary>
    private void ExpandRegionsFromAccessibleFacilities(HashSet<Guid> regionIds, HashSet<Guid> fullFacilityIds, HashSet<Guid> unitIds)
    {
        if (fullFacilityIds.Count == 0 && unitIds.Count == 0)
        {
            return;
        }

        foreach (var id in db.Facilities
                     .Where(f => !f.IsDeleted &&
                                 (fullFacilityIds.Contains(f.Id) ||
                                  db.FacilityUnits.Any(u => unitIds.Contains(u.Id) && u.FacilityId == f.Id && !u.IsDeleted)))
                     .Select(f => f.RegionId)
                     .Distinct()
                     .ToList())
        {
            regionIds.Add(id);
        }
    }

    private async Task ExpandRegionsFromAccessibleFacilitiesAsync(
        HashSet<Guid> regionIds,
        HashSet<Guid> fullFacilityIds,
        HashSet<Guid> unitIds,
        CancellationToken cancellationToken)
    {
        if (fullFacilityIds.Count == 0 && unitIds.Count == 0)
        {
            return;
        }

        var facilityRegionIds = await db.Facilities
            .Where(f => !f.IsDeleted &&
                        (fullFacilityIds.Contains(f.Id) ||
                         db.FacilityUnits.Any(u => unitIds.Contains(u.Id) && u.FacilityId == f.Id && !u.IsDeleted)))
            .Select(f => f.RegionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in facilityRegionIds)
        {
            regionIds.Add(id);
        }
    }

    private static void EnsureNoIds(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق Global/Headquarters لا يقبل معرفات منطقة أو سجن أو وحدة.");
        }
    }

    private static void EnsureRegionOnly(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق المنطقة يتطلب RegionId فقط.");
        }
    }

    private static void EnsureFacilityShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || !facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق السجن يتطلب RegionId وFacilityId دون FacilityUnitId.");
        }
    }

    private static void EnsureUnitShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || !facilityId.HasValue || !facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب RegionId وFacilityId وFacilityUnitId.");
        }
    }

    private async Task EnsureRegionExistsAsync(Guid regionId, CancellationToken cancellationToken)
    {
        if (!await db.Regions.AnyAsync(r => r.Id == regionId && !r.IsDeleted && r.IsActive, cancellationToken))
        {
            throw new KeyNotFoundException("المنطقة غير موجودة.");
        }
    }

    private async Task EnsureFacilityConsistentAsync(Guid? regionId, Guid facilityId, CancellationToken cancellationToken)
    {
        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == facilityId && !f.IsDeleted && f.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("السجن غير موجود.");

        if (regionId.HasValue && facility.RegionId != regionId.Value)
        {
            throw new InvalidOperationException("المنطقة لا تطابق السجن المحدد.");
        }
    }

    private async Task EnsureUnitBelongsToFacilityAsync(Guid facilityId, Guid facilityUnitId, CancellationToken cancellationToken)
    {
        var unit = await db.FacilityUnits.FirstOrDefaultAsync(u => u.Id == facilityUnitId && !u.IsDeleted && u.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("الوحدة غير موجودة.");

        if (unit.FacilityId != facilityId)
        {
            throw new InvalidOperationException("الوحدة لا تتبع السجن المحدد.");
        }
    }
}
