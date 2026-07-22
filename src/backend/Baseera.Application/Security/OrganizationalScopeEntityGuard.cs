namespace Baseera.Application.Security;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Shared org-entity existence/consistency checks for scoped create/update flows.
/// </summary>
public static class OrganizationalScopeEntityGuard
{
    public static async Task EnsureActiveAsync(
        IBaseeraDbContext db,
        ScopeType scopeType,
        Guid? regionId,
        Guid? facilityId,
        Guid? facilityUnitId,
        CancellationToken cancellationToken = default)
    {
        if (scopeType == ScopeType.Region && regionId.HasValue)
        {
            if (!await db.Regions.AnyAsync(r => r.Id == regionId.Value && !r.IsDeleted && r.IsActive, cancellationToken))
            {
                throw new KeyNotFoundException("المنطقة غير موجودة.");
            }
        }

        if (facilityId.HasValue)
        {
            var facility = await db.Facilities.FirstOrDefaultAsync(
                f => f.Id == facilityId.Value && !f.IsDeleted && f.IsActive,
                cancellationToken)
                ?? throw new KeyNotFoundException("السجن غير موجود.");

            if (regionId.HasValue && facility.RegionId != regionId.Value)
            {
                throw new InvalidOperationException("المنطقة لا تطابق السجن المحدد.");
            }
        }

        if (facilityUnitId.HasValue)
        {
            if (!facilityId.HasValue)
            {
                throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
            }

            var unit = await db.FacilityUnits.FirstOrDefaultAsync(
                u => u.Id == facilityUnitId.Value && !u.IsDeleted && u.IsActive,
                cancellationToken)
                ?? throw new KeyNotFoundException("الوحدة غير موجودة.");

            if (unit.FacilityId != facilityId.Value)
            {
                throw new InvalidOperationException("الوحدة لا تتبع السجن المحدد.");
            }
        }
    }
}
