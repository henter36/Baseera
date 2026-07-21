namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Notes;
using Microsoft.EntityFrameworkCore;

public interface INoteScopeService
{
    bool CanAccess(OperationalNote note);
    bool CanAccessRoutingRule(NoteRoutingRule rule);
    IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query);
    Task<IQueryable<OperationalNote>> FilterQueryableAsync(IQueryable<OperationalNote> query, CancellationToken cancellationToken = default);
    IQueryable<NoteRoutingRule> FilterRoutingRulesQueryable(IQueryable<NoteRoutingRule> query);
    Task<IQueryable<NoteRoutingRule>> FilterRoutingRulesQueryableAsync(IQueryable<NoteRoutingRule> query, CancellationToken cancellationToken = default);
    void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId);
    Task EnsureOrgEntitiesActiveAsync(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId, CancellationToken cancellationToken = default);
    Task<(Guid RegionId, Guid FacilityId)> ResolveIntakeAsync(Guid userId, Guid? requestedRegionId, Guid? requestedFacilityId, CancellationToken cancellationToken = default);
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

    public bool CanAccessRoutingRule(NoteRoutingRule rule) => orgScope.CanAccess(rule);

    public IQueryable<OperationalNote> FilterQueryable(IQueryable<OperationalNote> query)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterWithScopeIds(query, BuildAccessibleScopeIds());
    }

    public async Task<IQueryable<OperationalNote>> FilterQueryableAsync(
        IQueryable<OperationalNote> query,
        CancellationToken cancellationToken = default)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterWithScopeIds(query, await BuildAccessibleScopeIdsAsync(cancellationToken));
    }

    public IQueryable<NoteRoutingRule> FilterRoutingRulesQueryable(IQueryable<NoteRoutingRule> query)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterRoutingRulesWithScopeIds(query, BuildAccessibleScopeIds());
    }

    public async Task<IQueryable<NoteRoutingRule>> FilterRoutingRulesQueryableAsync(
        IQueryable<NoteRoutingRule> query,
        CancellationToken cancellationToken = default)
    {
        if (orgScope.HasNationalAccess)
        {
            return query;
        }

        return FilterRoutingRulesWithScopeIds(query, await BuildAccessibleScopeIdsAsync(cancellationToken));
    }

    public async Task<(Guid RegionId, Guid FacilityId)> ResolveIntakeAsync(
        Guid userId,
        Guid? requestedRegionId,
        Guid? requestedFacilityId,
        CancellationToken cancellationToken = default)
    {
        if (!requestedRegionId.HasValue || !requestedFacilityId.HasValue)
        {
            throw new InvalidOperationException("يجب اختيار المنطقة ثم الموقع.");
        }

        var facility = await db.Facilities.FirstOrDefaultAsync(f => f.Id == requestedFacilityId.Value && f.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("الموقع غير موجود.");
        if (facility.RegionId != requestedRegionId.Value)
        {
            throw new InvalidOperationException("الموقع لا يتبع المنطقة المحددة.");
        }

        var probe = new OperationalNote { ScopeType = ScopeType.Facility, RegionId = facility.RegionId, FacilityId = facility.Id };
        if (!orgScope.CanAccess(probe))
        {
            throw new UnauthorizedAccessException("الموقع خارج نطاق المستخدم.");
        }

        var profile = await db.UserNoteIntakeProfiles.FirstOrDefaultAsync(p => p.UserId == userId && p.IsActive, cancellationToken);
        if (profile?.LockType == NoteIntakeLockType.Region && profile.RegionId != facility.RegionId)
        {
            throw new InvalidOperationException("المنطقة المحددة لا تطابق منطقة الإدخال المثبتة.");
        }

        if (profile?.LockType == NoteIntakeLockType.Facility && profile.FacilityId != facility.Id)
        {
            throw new InvalidOperationException("الموقع المحدد لا يطابق موقع الإدخال المثبت.");
        }

        return (facility.RegionId, facility.Id);
    }

    private IQueryable<OperationalNote> FilterWithScopeIds(
        IQueryable<OperationalNote> query,
        (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) ids)
    {
        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var (regionIds, facilityIds, unitIds) = ids;
        var hasHq = orgScope.HasHeadquartersAccess;

        return query.Where(n =>
            (n.ScopeType == ScopeType.Headquarters && hasHq) ||
            (n.ScopeType == ScopeType.Region && n.RegionId.HasValue && regionIds.Contains(n.RegionId.Value)) ||
            (n.ScopeType == ScopeType.Facility && n.FacilityId.HasValue && facilityIds.Contains(n.FacilityId.Value)) ||
            (n.ScopeType == ScopeType.FacilityUnit && (
                (n.FacilityUnitId.HasValue && unitIds.Contains(n.FacilityUnitId.Value)) ||
                (n.FacilityId.HasValue && facilityIds.Contains(n.FacilityId.Value) && unitIds.Count == 0))));
    }

    private IQueryable<NoteRoutingRule> FilterRoutingRulesWithScopeIds(
        IQueryable<NoteRoutingRule> query,
        (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) ids)
    {
        if (!currentUser.IsAuthenticated || currentUser.Scopes.Count == 0)
        {
            return query.Where(_ => false);
        }

        var (regionIds, facilityIds, unitIds) = ids;
        var hasHq = orgScope.HasHeadquartersAccess;
        var hasDirectRegionScope = currentUser.Scopes.Any(scope =>
            scope.ScopeType is ScopeType.Region or ScopeType.MultipleRegions);
        var hasDirectFacilityScope = currentUser.Scopes.Any(scope =>
            scope.ScopeType is ScopeType.Facility or ScopeType.MultipleFacilities);
        var hasUnitScope = unitIds.Count > 0;
        var allowFacilityUnitRulesUnderAccessibleFacilities = hasDirectRegionScope || hasDirectFacilityScope;

        return query.Where(rule =>
            (rule.ScopeType == ScopeType.Headquarters && hasHq) ||
            (hasDirectRegionScope &&
             rule.ScopeType == ScopeType.Region &&
             rule.RegionId.HasValue &&
             regionIds.Contains(rule.RegionId.Value)) ||
            (rule.ScopeType == ScopeType.Facility &&
             rule.FacilityId.HasValue &&
             facilityIds.Contains(rule.FacilityId.Value) &&
             (hasDirectRegionScope || hasDirectFacilityScope)) ||
            (rule.ScopeType == ScopeType.FacilityUnit &&
             rule.FacilityUnitId.HasValue &&
             (unitIds.Contains(rule.FacilityUnitId.Value) ||
              (!hasUnitScope &&
               rule.FacilityId.HasValue &&
               facilityIds.Contains(rule.FacilityId.Value) &&
               allowFacilityUnitRulesUnderAccessibleFacilities))));
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
