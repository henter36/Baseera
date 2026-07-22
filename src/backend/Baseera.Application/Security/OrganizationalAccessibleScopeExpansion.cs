namespace Baseera.Application.Security;

using Baseera.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared facility/region expansion used by Notes and Forms scope filters.
/// </summary>
public static class OrganizationalAccessibleScopeExpansion
{
    public static void ExpandFacilitiesFromRegions(
        IBaseeraDbContext db,
        HashSet<Guid> regionIds,
        HashSet<Guid> facilityIds)
    {
        foreach (var id in db.Facilities
                     .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
                     .Select(f => f.Id)
                     .ToList())
        {
            facilityIds.Add(id);
        }
    }

    public static async Task ExpandFacilitiesFromRegionsAsync(
        IBaseeraDbContext db,
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

    public static void ExpandRegionsFromFacilities(
        IBaseeraDbContext db,
        HashSet<Guid> regionIds,
        HashSet<Guid> facilityIds)
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

    public static async Task ExpandRegionsFromFacilitiesAsync(
        IBaseeraDbContext db,
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
}
