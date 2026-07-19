namespace Baseera.Application.Security;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Organization;

public sealed class OrganizationalScopeService(ICurrentUser currentUser, IBaseeraDbContext db) : IOrganizationalScopeService
{
    public bool HasNationalAccess =>
        currentUser.IsAuthenticated &&
        currentUser.Scopes.Any(s => s.ScopeType == ScopeType.Global);

    public bool HasHeadquartersAccess =>
        HasNationalAccess ||
        currentUser.Scopes.Any(s => s.ScopeType == ScopeType.Headquarters);

    public bool CanAccessRegion(Guid regionId)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        if (HasNationalAccess)
        {
            return true;
        }

        return currentUser.Scopes.Any(s =>
            (s.ScopeType == ScopeType.Region && s.RegionId == regionId) ||
            (s.ScopeType == ScopeType.MultipleRegions && s.RegionId == regionId) ||
            ((s.ScopeType == ScopeType.Facility || s.ScopeType == ScopeType.FacilityUnit || s.ScopeType == ScopeType.MultipleFacilities) &&
             s.FacilityId.HasValue &&
             db.Facilities.Any(f => f.Id == s.FacilityId && f.RegionId == regionId && !f.IsDeleted)));
    }

    public bool CanAccessFacility(Guid facilityId)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        if (HasNationalAccess)
        {
            return true;
        }

        var facility = db.Facilities.FirstOrDefault(f => f.Id == facilityId && !f.IsDeleted);
        if (facility is null)
        {
            return false;
        }

        return currentUser.Scopes.Any(s =>
            ((s.ScopeType == ScopeType.Facility || s.ScopeType == ScopeType.MultipleFacilities) && s.FacilityId == facilityId) ||
            ((s.ScopeType == ScopeType.Region || s.ScopeType == ScopeType.MultipleRegions) && s.RegionId == facility.RegionId) ||
            (s.ScopeType == ScopeType.FacilityUnit && s.FacilityId == facilityId));
    }

    public bool CanAccessFacilityUnit(Guid facilityUnitId)
    {
        if (!currentUser.IsAuthenticated)
        {
            return false;
        }

        if (HasNationalAccess)
        {
            return true;
        }

        var unit = db.FacilityUnits.FirstOrDefault(u => u.Id == facilityUnitId && !u.IsDeleted);
        if (unit is null)
        {
            return false;
        }

        if (currentUser.Scopes.Any(s => s.ScopeType == ScopeType.FacilityUnit && s.FacilityUnitId == facilityUnitId))
        {
            return true;
        }

        return CanAccessFacility(unit.FacilityId);
    }

    public IQueryable<Region> FilterRegions(IQueryable<Region> query)
    {
        if (HasNationalAccess)
        {
            return query;
        }

        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue && (s.ScopeType == ScopeType.Region || s.ScopeType == ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        var facilityIdsFromScopes = currentUser.Scopes
            .Where(s => s.FacilityId.HasValue &&
                        (s.ScopeType == ScopeType.Facility ||
                         s.ScopeType == ScopeType.MultipleFacilities ||
                         s.ScopeType == ScopeType.FacilityUnit))
            .Select(s => s.FacilityId!.Value)
            .ToHashSet();

        var facilityRegionIds = db.Facilities
            .Where(f => facilityIdsFromScopes.Contains(f.Id) && !f.IsDeleted)
            .Select(f => f.RegionId)
            .Distinct()
            .ToList();

        foreach (var id in facilityRegionIds)
        {
            regionIds.Add(id);
        }

        return query.Where(r => regionIds.Contains(r.Id));
    }

    public IQueryable<Facility> FilterFacilities(IQueryable<Facility> query)
    {
        if (HasNationalAccess)
        {
            return query;
        }

        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var facilityIds = currentUser.Scopes
            .Where(s => s.FacilityId.HasValue &&
                        (s.ScopeType == ScopeType.Facility ||
                         s.ScopeType == ScopeType.MultipleFacilities ||
                         s.ScopeType == ScopeType.FacilityUnit))
            .Select(s => s.FacilityId!.Value)
            .ToHashSet();

        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue && (s.ScopeType == ScopeType.Region || s.ScopeType == ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        return query.Where(f => facilityIds.Contains(f.Id) || regionIds.Contains(f.RegionId));
    }

    public bool CanAccess(IScopedEntity entity) =>
        entity.ScopeType switch
        {
            ScopeType.Global => HasNationalAccess,
            ScopeType.Headquarters => HasHeadquartersAccess,
            ScopeType.Region => entity.RegionId is Guid id && CanAccessRegion(id),
            ScopeType.Facility or ScopeType.MultipleFacilities => entity.FacilityId is Guid id && CanAccessFacility(id),
            ScopeType.FacilityUnit => entity.FacilityUnitId is Guid id && CanAccessFacilityUnit(id),
            ScopeType.MultipleRegions => entity.RegionId is Guid id && CanAccessRegion(id),
            _ => false
        };

    public string SummarizeScopes()
    {
        if (HasNationalAccess)
        {
            return "Global";
        }

        if (HasHeadquartersAccess)
        {
            return "Headquarters";
        }

        return string.Join("; ", currentUser.Scopes.Select(s =>
            $"{s.ScopeType}:{s.RegionId}:{s.FacilityId}:{s.FacilityUnitId}"));
    }
}
