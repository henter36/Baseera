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
        (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) ids)
    {
        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var (regionIds, facilityIds, unitIds) = ids;
        var hasHq = orgScope.HasHeadquartersAccess;

        return query.Where(f =>
            (f.ScopeType == ScopeType.Headquarters && hasHq) ||
            (f.ScopeType == ScopeType.Region && f.RegionId.HasValue && regionIds.Contains(f.RegionId.Value)) ||
            (f.ScopeType == ScopeType.Facility && f.FacilityId.HasValue && facilityIds.Contains(f.FacilityId.Value)) ||
            (f.ScopeType == ScopeType.FacilityUnit && (
                (f.FacilityUnitId.HasValue && unitIds.Contains(f.FacilityUnitId.Value)) ||
                (f.FacilityId.HasValue && facilityIds.Contains(f.FacilityId.Value) && unitIds.Count == 0))));
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) BuildAccessibleScopeIds()
    {
        var ids = CollectScopeIdsFromUser();
        ExpandRegionsFromAccessibleFacilities(ids.RegionIds, ids.FacilityIds);
        ExpandFacilitiesFromAccessibleRegions(ids.RegionIds, ids.FacilityIds);
        return ids;
    }

    private async Task<(HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds)> BuildAccessibleScopeIdsAsync(
        CancellationToken cancellationToken)
    {
        var ids = CollectScopeIdsFromUser();
        await ExpandRegionsFromAccessibleFacilitiesAsync(ids.RegionIds, ids.FacilityIds, cancellationToken);
        await ExpandFacilitiesFromAccessibleRegionsAsync(ids.RegionIds, ids.FacilityIds, cancellationToken);
        return ids;
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) CollectScopeIdsFromUser()
    {
        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue &&
                        (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        var facilityIds = currentUser.Scopes
            .Where(s => s.FacilityId.HasValue &&
                        (s.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities or ScopeType.FacilityUnit))
            .Select(s => s.FacilityId!.Value)
            .ToHashSet();

        var unitIds = currentUser.Scopes
            .Where(s => s.FacilityUnitId.HasValue && s.ScopeType == ScopeType.FacilityUnit)
            .Select(s => s.FacilityUnitId!.Value)
            .ToHashSet();

        return (regionIds, facilityIds, unitIds);
    }

    private void ExpandRegionsFromAccessibleFacilities(HashSet<Guid> regionIds, HashSet<Guid> facilityIds)
    {
        foreach (var id in db.Facilities
                     .Where(f => facilityIds.Contains(f.Id) && !f.IsDeleted)
                     .Select(f => f.RegionId)
                     .Distinct()
                     .ToList())
        {
            regionIds.Add(id);
        }
    }

    private async Task ExpandRegionsFromAccessibleFacilitiesAsync(
        HashSet<Guid> regionIds,
        HashSet<Guid> facilityIds,
        CancellationToken cancellationToken)
    {
        var facilityRegionIds = await db.Facilities
            .Where(f => facilityIds.Contains(f.Id) && !f.IsDeleted)
            .Select(f => f.RegionId)
            .Distinct()
            .ToListAsync(cancellationToken);
        foreach (var id in facilityRegionIds)
        {
            regionIds.Add(id);
        }
    }

    private void ExpandFacilitiesFromAccessibleRegions(HashSet<Guid> regionIds, HashSet<Guid> facilityIds)
    {
        foreach (var id in db.Facilities
                     .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
                     .Select(f => f.Id)
                     .ToList())
        {
            facilityIds.Add(id);
        }
    }

    private async Task ExpandFacilitiesFromAccessibleRegionsAsync(
        HashSet<Guid> regionIds,
        HashSet<Guid> facilityIds,
        CancellationToken cancellationToken)
    {
        var regionFacilityIds = await db.Facilities
            .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
            .Select(f => f.Id)
            .ToListAsync(cancellationToken);
        foreach (var id in regionFacilityIds)
        {
            facilityIds.Add(id);
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
