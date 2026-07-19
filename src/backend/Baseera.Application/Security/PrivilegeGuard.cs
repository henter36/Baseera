namespace Baseera.Application.Security;

using Baseera.Application.Abstractions;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;

public static class RoleHierarchy
{
    private static readonly Dictionary<string, int> Ranks = new(StringComparer.OrdinalIgnoreCase)
    {
        [RoleCodes.SystemAdministrator] = 1000,
        [RoleCodes.HeadquartersExecutive] = 900,
        [RoleCodes.DecisionSupportDirector] = 850,
        [RoleCodes.DecisionAnalyst] = 700,
        [RoleCodes.RegionalDirector] = 600,
        [RoleCodes.RegionalCoordinator] = 550,
        [RoleCodes.FacilityDirector] = 500,
        [RoleCodes.FacilityCoordinator] = 450,
        [RoleCodes.ProjectManager] = 400,
        [RoleCodes.StrategyOfficer] = 400,
        [RoleCodes.FormDesigner] = 350,
        [RoleCodes.FormReviewer] = 340,
        [RoleCodes.SecurityOfficer] = 300,
        [RoleCodes.ArmamentOfficer] = 300,
        [RoleCodes.FleetOfficer] = 300,
        [RoleCodes.WorkforceOfficer] = 300,
        [RoleCodes.IncidentOfficer] = 300,
        [RoleCodes.PrisonerCaseOfficer] = 300,
        [RoleCodes.Auditor] = 250,
        [RoleCodes.ReadOnlyUser] = 100
    };

    private static readonly HashSet<string> ScopeAdminRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        RoleCodes.SystemAdministrator,
        RoleCodes.HeadquartersExecutive,
        RoleCodes.DecisionSupportDirector
    };

    public static int RankOf(string roleCode) =>
        Ranks.TryGetValue(roleCode, out var rank) ? rank : 0;

    public static int MaxRank(IEnumerable<string> roleCodes) =>
        roleCodes.Select(RankOf).DefaultIfEmpty(0).Max();

    public static bool IsScopeAdminRole(IEnumerable<string> roleCodes) =>
        roleCodes.Any(r => ScopeAdminRoles.Contains(r));

    public static bool IsSystemAdministrator(IEnumerable<string> roleCodes) =>
        roleCodes.Contains(RoleCodes.SystemAdministrator, StringComparer.OrdinalIgnoreCase);
}

public interface IPrivilegeGuard
{
    void EnsureCanAssignRole(Guid targetUserId, string roleCode);
    void EnsureCanAssignScope(Guid targetUserId, ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId);
}

public sealed class PrivilegeGuard(
    ICurrentUser currentUser,
    IBaseeraDbContext db,
    IOrganizationalScopeService scopeService) : IPrivilegeGuard
{
    public void EnsureCanAssignRole(Guid targetUserId, string roleCode)
    {
        EnsureHasPermission(PermissionCodes.RolesManage, "ليست لديك صلاحية إدارة الأدوار.");
        EnsureTargetIsNotSelf(targetUserId);
        EnsureTargetUserAssignable(targetUserId);

        var actorRoles = LoadActorRoles();
        EnsureRoleRankAllowsGrant(actorRoles, roleCode);
        EnsureActorHoldsRolePermissions(actorRoles, roleCode);
    }

    public void EnsureCanAssignScope(Guid targetUserId, ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        EnsureHasPermission(PermissionCodes.ScopesManage, "ليست لديك صلاحية إدارة النطاقات.");
        EnsureTargetIsNotSelf(targetUserId);
        EnsureTargetUserAssignable(targetUserId);
        ValidateScopeShape(scopeType, regionId, facilityId, facilityUnitId);

        var actorRoles = LoadActorRoles();
        switch (scopeType)
        {
            case ScopeType.Global:
                EnsureCanGrantGlobalScope(actorRoles);
                return;
            case ScopeType.Headquarters:
                EnsureCanGrantHeadquartersScope(actorRoles);
                return;
            case ScopeType.Region:
            case ScopeType.MultipleRegions:
                EnsureCanGrantRegionScope(regionId);
                return;
            case ScopeType.Facility:
            case ScopeType.MultipleFacilities:
                EnsureCanGrantFacilityScope(regionId, facilityId);
                return;
            case ScopeType.FacilityUnit:
                EnsureCanGrantFacilityUnitScope(facilityId, facilityUnitId);
                return;
            default:
                throw new InvalidOperationException("نوع النطاق غير مدعوم.");
        }
    }

    private void EnsureCanGrantGlobalScope(IReadOnlyCollection<string> actorRoles)
    {
        if (!currentUser.HasPermission(PermissionCodes.GrantGlobalScope))
        {
            throw new UnauthorizedAccessException("منح النطاق الوطني يتطلب صلاحية Scopes.GrantGlobal.");
        }

        if (!currentUser.IsGlobalScope)
        {
            throw new UnauthorizedAccessException("منح النطاق الوطني يتطلب أن تملك نطاق Global.");
        }

        if (!RoleHierarchy.IsScopeAdminRole(actorRoles))
        {
            throw new UnauthorizedAccessException("منح النطاق الوطني مقصور على دور إداري معتمد.");
        }
    }

    private void EnsureCanGrantHeadquartersScope(IReadOnlyCollection<string> actorRoles)
    {
        if (!currentUser.HasPermission(PermissionCodes.GrantHeadquartersScope))
        {
            throw new UnauthorizedAccessException("منح نطاق المستوى الرئيسي يتطلب صلاحية Scopes.GrantHeadquarters.");
        }

        if (!currentUser.IsGlobalScope && !currentUser.HasHeadquartersScope)
        {
            throw new UnauthorizedAccessException("منح نطاق المستوى الرئيسي يتطلب نطاق Global أو Headquarters.");
        }

        if (!RoleHierarchy.IsScopeAdminRole(actorRoles))
        {
            throw new UnauthorizedAccessException("منح نطاق المستوى الرئيسي مقصور على دور إداري معتمد.");
        }
    }

    private void EnsureCanGrantRegionScope(Guid? regionId)
    {
        if (regionId is null || !scopeService.CanAccessRegion(regionId.Value))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح نطاق خارج منطقتك.");
        }
    }

    private void EnsureCanGrantFacilityScope(Guid? regionId, Guid? facilityId)
    {
        if (facilityId is null || !scopeService.CanAccessFacility(facilityId.Value))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح نطاق خارج سجنك.");
        }

        EnsureFacilityBelongsToRegion(facilityId.Value, regionId);
    }

    private void EnsureCanGrantFacilityUnitScope(Guid? facilityId, Guid? facilityUnitId)
    {
        if (facilityId is null || !scopeService.CanAccessFacility(facilityId.Value))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح نطاق خارج سجنك.");
        }

        if (facilityUnitId is null || !scopeService.CanAccessFacilityUnit(facilityUnitId.Value))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح نطاق وحدة خارج صلاحيتك.");
        }

        EnsureUnitBelongsToFacility(facilityUnitId.Value, facilityId.Value);
    }

    private void EnsureFacilityBelongsToRegion(Guid facilityId, Guid? regionId)
    {
        var facility = db.Facilities.FirstOrDefault(f => f.Id == facilityId && !f.IsDeleted)
            ?? throw new KeyNotFoundException("السجن غير موجود.");
        if (regionId.HasValue && facility.RegionId != regionId.Value)
        {
            throw new InvalidOperationException("السجن لا يتبع المنطقة المحددة.");
        }
    }

    private void EnsureUnitBelongsToFacility(Guid facilityUnitId, Guid facilityId)
    {
        var unit = db.FacilityUnits.FirstOrDefault(u => u.Id == facilityUnitId && !u.IsDeleted)
            ?? throw new KeyNotFoundException("الوحدة غير موجودة.");
        if (unit.FacilityId != facilityId)
        {
            throw new InvalidOperationException("الوحدة لا تتبع السجن المحدد.");
        }
    }

    private void EnsureHasPermission(string permission, string message)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new UnauthorizedAccessException(message);
        }
    }

    private void EnsureTargetIsNotSelf(Guid targetUserId)
    {
        if (currentUser.UserId == targetUserId)
        {
            throw new UnauthorizedAccessException("لا يمكنك تعديل أدوارك أو نطاقاتك بنفسك.");
        }
    }

    private static void EnsureRoleRankAllowsGrant(IReadOnlyCollection<string> actorRoles, string roleCode)
    {
        var actorRank = RoleHierarchy.MaxRank(actorRoles);
        var targetRank = RoleHierarchy.RankOf(roleCode);
        var isSysAdmin = RoleHierarchy.IsSystemAdministrator(actorRoles);

        if (targetRank > actorRank || (targetRank >= actorRank && !isSysAdmin))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح دورًا بمستوى يساوي أو أعلى من مستواك.");
        }
    }

    private void EnsureActorHoldsRolePermissions(IReadOnlyCollection<string> actorRoles, string roleCode)
    {
        if (RoleHierarchy.IsSystemAdministrator(actorRoles))
        {
            return;
        }

        var rolePermissions = (
            from r in db.Roles
            join rp in db.RolePermissions on r.Id equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where r.Code == roleCode && !r.IsDeleted
            select p.Code).Distinct().ToList();

        var missing = rolePermissions.Where(permission => !currentUser.HasPermission(permission)).ToArray();
        if (missing.Length > 0)
        {
            throw new UnauthorizedAccessException($"لا يمكنك منح صلاحية لا تمتلكها: {missing[0]}");
        }
    }

    private void EnsureTargetUserAssignable(Guid targetUserId)
    {
        var target = db.UsersIncludingDeleted.FirstOrDefault(u => u.Id == targetUserId)
            ?? throw new KeyNotFoundException("المستخدم غير موجود.");

        if (target.IsDeleted)
        {
            throw new UnauthorizedAccessException("لا يمكن منح أدوار أو نطاقات لمستخدم مؤرشف.");
        }

        if (!target.IsActive || target.ProvisioningStatus is UserProvisioningStatus.Disabled)
        {
            throw new UnauthorizedAccessException("لا يمكن منح أدوار أو نطاقات لمستخدم غير نشط.");
        }
    }

    private List<string> LoadActorRoles() =>
        db.UserRoles
            .Where(ur => ur.UserId == currentUser.UserId)
            .Join(db.Roles.Where(r => !r.IsDeleted), ur => ur.RoleId, r => r.Id, (_, r) => r.Code)
            .ToList();

    public static void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        switch (scopeType)
        {
            case ScopeType.Global:
            case ScopeType.Headquarters:
                ValidateNationalScopeShape(regionId, facilityId, facilityUnitId);
                return;
            case ScopeType.Region:
            case ScopeType.MultipleRegions:
                ValidateRegionScopeShape(regionId, facilityId, facilityUnitId);
                return;
            case ScopeType.Facility:
            case ScopeType.MultipleFacilities:
                ValidateFacilityScopeShape(facilityId, facilityUnitId);
                return;
            case ScopeType.FacilityUnit:
                ValidateFacilityUnitScopeShape(facilityId, facilityUnitId);
                return;
            default:
                throw new InvalidOperationException("نوع النطاق غير مدعوم.");
        }
    }

    private static void ValidateNationalScopeShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق Global/Headquarters لا يقبل معرفات منطقة أو سجن أو وحدة.");
        }
    }

    private static void ValidateRegionScopeShape(Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق المنطقة يتطلب RegionId فقط.");
        }
    }

    private static void ValidateFacilityScopeShape(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق السجن يتطلب FacilityId.");
        }
    }

    private static void ValidateFacilityUnitScopeShape(Guid? facilityId, Guid? facilityUnitId)
    {
        if (!facilityId.HasValue || !facilityUnitId.HasValue)
        {
            throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
        }
    }
}
