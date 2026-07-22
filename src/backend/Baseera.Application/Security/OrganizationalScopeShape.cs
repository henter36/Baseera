namespace Baseera.Application.Security;

using Baseera.Domain.Common;

/// <summary>
/// Shared create/update scope-shape rules for organizational entities (Notes, Forms, …).
/// </summary>
public static class OrganizationalScopeShape
{
    public static void EnsureNoIds(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق Global/Headquarters لا يقبل معرفات منطقة أو سجن أو وحدة.");
        }
    }

    public static void EnsureRegionOnly(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق المنطقة يتطلب RegionId فقط.");
        }
    }

    public static void EnsureFacilityShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || !facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق السجن يتطلب RegionId وFacilityId دون FacilityUnitId.");
        }
    }

    public static void EnsureFacilityShapeWithoutRegion(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق السجن يتطلب FacilityId دون FacilityUnitId.");
        }
    }

    public static void EnsureUnitShapeWithoutRegion(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || !facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
        }
    }

    public static void EnsureUnitShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || !facilityId.HasValue || !facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب RegionId وFacilityId وFacilityUnitId.");
        }
    }
}
