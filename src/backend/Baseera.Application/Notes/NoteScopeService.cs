namespace Baseera.Application.Notes;

using Baseera.Application.Abstractions;
using Baseera.Application.Security;
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

        var facility = await db.Facilities
            .FirstOrDefaultAsync(
                facility =>
                    facility.Id == requestedFacilityId.Value &&
                    facility.IsActive &&
                    !facility.IsDeleted,
                cancellationToken)
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

        // Expand facilities only from directly granted regions.
        // Facility-derived regions must not feed back into facility expansion.
        var directlyGrantedRegionIds = ids.RegionIds.ToHashSet();
        OrganizationalAccessibleScopeExpansion.ExpandFacilitiesFromRegions(
            db,
            directlyGrantedRegionIds,
            ids.FacilityIds);
        OrganizationalAccessibleScopeExpansion.ExpandRegionsFromFacilities(
            db,
            ids.RegionIds,
            ids.FacilityIds);

        return ids;
    }

    private async Task<(HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds)> BuildAccessibleScopeIdsAsync(
        CancellationToken cancellationToken)
    {
        var ids = CollectScopeIdsFromUser();

        // Expand facilities only from directly granted regions.
        // Facility-derived regions must not feed back into facility expansion.
        var directlyGrantedRegionIds = ids.RegionIds.ToHashSet();
        await OrganizationalAccessibleScopeExpansion.ExpandFacilitiesFromRegionsAsync(
            db,
            directlyGrantedRegionIds,
            ids.FacilityIds,
            cancellationToken);
        await OrganizationalAccessibleScopeExpansion.ExpandRegionsFromFacilitiesAsync(
            db,
            ids.RegionIds,
            ids.FacilityIds,
            cancellationToken);

        return ids;
    }

    private (HashSet<Guid> RegionIds, HashSet<Guid> FacilityIds, HashSet<Guid> UnitIds) CollectScopeIdsFromUser()
    {
        var regionIds = currentUser.Scopes
            .Where(s => s.RegionId.HasValue &&
                        (s.ScopeType is ScopeType.Region or ScopeType.MultipleRegions))
            .Select(s => s.RegionId!.Value)
            .ToHashSet();

        // Notes still promote FacilityUnit parent facilities into the facility set (legacy Phase B behavior).
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
                OrganizationalScopeShape.EnsureNoIds(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Region:
                OrganizationalScopeShape.EnsureRegionOnly(regionId, facilityId, facilityUnitId);
                break;
            case ScopeType.Facility:
                OrganizationalScopeShape.EnsureFacilityShapeWithoutRegion(facilityId, facilityUnitId);
                break;
            case ScopeType.FacilityUnit:
                OrganizationalScopeShape.EnsureUnitShapeWithoutRegion(facilityId, facilityUnitId);
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
            regionId,
            facilityId,
            facilityUnitId,
            cancellationToken);
}
