namespace Baseera.Application.Security;

using Baseera.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared org-entity existence/consistency checks for scoped create/update flows.
/// </summary>
public static class OrganizationalScopeEntityGuard
{
    public static async Task EnsureActiveAsync(
        IBaseeraDbContext db,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken = default)
    {
        await EnsureRegionActiveAsync(db, regionId, cancellationToken);
        await EnsureFacilityActiveAndConsistentAsync(db, regionId, facilityId, cancellationToken);
        await EnsureUnitActiveAndConsistentAsync(db, facilityId, facilityUnitId, cancellationToken);
    }

    private static async Task EnsureRegionActiveAsync(
        IBaseeraDbContext db,
        Guid? regionId,
        CancellationToken cancellationToken)
    {
        if (regionId is not Guid requiredRegionId)
        {
            return;
        }

        var exists = await db.Regions
            .AsNoTracking()
            .AnyAsync(
                region =>
                    region.Id == requiredRegionId &&
                    !region.IsDeleted &&
                    region.IsActive,
                cancellationToken);

        if (!exists)
        {
            throw new KeyNotFoundException("المنطقة غير موجودة.");
        }
    }

    private static async Task EnsureFacilityActiveAndConsistentAsync(
        IBaseeraDbContext db,
        Guid? regionId,
        Guid? facilityId,
        CancellationToken cancellationToken)
    {
        if (!facilityId.HasValue)
        {
            return;
        }

        var facility = await db.Facilities.AsNoTracking().FirstOrDefaultAsync(
            f => f.Id == facilityId.Value && !f.IsDeleted && f.IsActive,
            cancellationToken)
            ?? throw new KeyNotFoundException("السجن غير موجود.");

        if (regionId.HasValue && facility.RegionId != regionId.Value)
        {
            throw new InvalidOperationException("المنطقة لا تطابق السجن المحدد.");
        }
    }

    private static async Task EnsureUnitActiveAndConsistentAsync(
        IBaseeraDbContext db,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken)
    {
        if (!facilityUnitId.HasValue)
        {
            return;
        }

        if (!facilityId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
        }

        var unit = await db.FacilityUnits.AsNoTracking().FirstOrDefaultAsync(
            u => u.Id == facilityUnitId.Value && !u.IsDeleted && u.IsActive,
            cancellationToken)
            ?? throw new KeyNotFoundException("الوحدة غير موجودة.");

        if (unit.FacilityId != facilityId.Value)
        {
            throw new InvalidOperationException("الوحدة لا تتبع السجن المحدد.");
        }
    }
}
