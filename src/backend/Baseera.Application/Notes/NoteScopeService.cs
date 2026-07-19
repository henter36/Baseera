namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteScopeService
{
    bool CanAccess(OperationalNote note);
    IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query);
    void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId);
    Task EnsureOrgEntitiesActiveAsync(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId, CancellationToken cancellationToken = default);
}

public sealed class NoteScopeService(
    IOrganizationalScopeService orgScope,
    ICurrentUser currentUser,
    IBaseeraDbContext db) : INoteScopeService
{
    private static readonly ScopeType[] Supported =
    [
        ScopeType.Global,
        ScopeType.Headquarters,
        ScopeType.Region,
        ScopeType.Facility,
        ScopeType.FacilityUnit
    ];

    public bool CanAccess(OperationalNote note) => orgScope.CanAccess(note);

    public IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var (regionIds, facilityIds, unitIds) = BuildAccessibleScopeIds();
        var hasHq = orgScope.HasHeadquartersAccess;

        return query.Where(n =>
            (n.ScopeType == ScopeType.Headquarters && hasHq) ||
            (n.ScopeType == ScopeType.Region && n.RegionId.HasValue && regionIds.Contains(n.RegionId.Value)) ||
            (n.ScopeType == ScopeType.Facility && n.FacilityId.HasValue && facilityIds.Contains(n.FacilityId.Value)) ||
            (n.ScopeType == ScopeType.FacilityUnit && (
                (n.FacilityUnitId.HasValue && unitIds.Contains(n.FacilityUnitId.Value)) ||
                (n.FacilityId.HasValue && facilityIds.Contains(n.FacilityId.Value) && unitIds.Count == 0))));
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) BuildAccessibleScopeIds()
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

        ExpandRegionsFromAccessibleFacilities(regionIds, facilityIds);
        ExpandFacilitiesFromAccessibleRegions(regionIds, facilityIds);

        return (regionIds, facilityIds, unitIds);
    }

    private void ExpandRegionsFromAccessibleFacilities(HashSet<Guid> regionIds, HashSet<Guid> facilityIds)
    {
        var facilityRegionIds = db.Facilities
            .Where(f => facilityIds.Contains(f.Id) && !f.IsDeleted)
            .Select(f => f.RegionId)
            .Distinct()
            .ToList();
        foreach (var id in facilityRegionIds)
        {
            regionIds.Add(id);
        }
    }

    private void ExpandFacilitiesFromAccessibleRegions(HashSet<Guid> regionIds, HashSet<Guid> facilityIds)
    {
        var regionFacilityIds = db.Facilities
            .Where(f => regionIds.Contains(f.RegionId) && !f.IsDeleted)
            .Select(f => f.Id)
            .ToList();
        foreach (var id in regionFacilityIds)
        {
            facilityIds.Add(id);
        }
    }

    public void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!Supported.Contains(scopeType))
        {
            throw new InvalidOperationException("نطاق الملاحظة غير مدعوم في هذه المرحلة.");
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
                EnsureFacilityShape(facilityId, facilityUnitId);
                break;
            case ScopeType.FacilityUnit:
                EnsureUnitShape(facilityId, facilityUnitId);
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

    private static void EnsureFacilityShape(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق السجن يتطلب FacilityId دون FacilityUnitId.");
        }
    }

    private static void EnsureUnitShape(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || !facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
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
