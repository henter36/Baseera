namespace Baseera.Application.Forms;

using Baseera.Application.Abstractions;
using Baseera.Application.Security;
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
                OrganizationalScopeShape.EnsureNoIds(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Region:
                OrganizationalScopeShape.EnsureRegionOnly(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Facility:
                OrganizationalScopeShape.EnsureFacilityShape(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.FacilityUnit:
                OrganizationalScopeShape.EnsureUnitShape(regionId, facilityId, facilityUnitId);
                break;
        }
    }

    public Task EnsureOrgEntitiesActiveAsync(
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken = default) =>
        OrganizationalScopeEntityGuard.EnsureActiveAsync(
            db,
            scopeType,
            regionId,
            facilityId,
            facilityUnitId,
            cancellationToken);

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
        OrganizationalAccessibleScopeExpansion.ExpandFacilitiesFromRegions(db, ids.RegionIds, ids.FullFacilityIds);
        ExpandRegionsIncludingUnitFacilities(ids.RegionIds, ids.FullFacilityIds, ids.UnitIds);
        return ids;
    }

    private async Task<(HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds)> BuildAccessibleScopeIdsAsync(
        CancellationToken cancellationToken)
    {
        var ids = CollectScopeIdsFromUser();
        await OrganizationalAccessibleScopeExpansion.ExpandFacilitiesFromRegionsAsync(
            db, ids.RegionIds, ids.FullFacilityIds, cancellationToken);
        await ExpandRegionsIncludingUnitFacilitiesAsync(
            ids.RegionIds, ids.FullFacilityIds, ids.UnitIds, cancellationToken);
        return ids;
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FullFacilityIds, HashSet<Guid> UnitIds) CollectScopeIdsFromUser()
    {
        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue &&
                        (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        // FacilityUnit scopes intentionally do NOT promote the parent facility to full access.
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
    /// Unit-only users still need Region-scoped forms for the region that owns their unit's facility.
    /// </summary>
    private void ExpandRegionsIncludingUnitFacilities(
        HashSet<Guid> regionIds,
        HashSet<Guid> fullFacilityIds,
        HashSet<Guid> unitIds)
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

    private async Task ExpandRegionsIncludingUnitFacilitiesAsync(
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
}
