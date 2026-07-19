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

    public static int RankOf(string roleCode) =>
        Ranks.TryGetValue(roleCode, out var rank) ? rank : 0;

    public static int MaxRank(IEnumerable<string> roleCodes) =>
        roleCodes.Select(RankOf).DefaultIfEmpty(0).Max();
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
        if (!currentUser.HasPermission(PermissionCodes.RolesManage))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية إدارة الأدوار.");
        }

        if (currentUser.UserId == targetUserId)
        {
            throw new UnauthorizedAccessException("لا يمكنك تعديل أدوارك بنفسك.");
        }

        var actorRoles = db.UserRoles
            .Where(ur => ur.UserId == currentUser.UserId)
            .Join(db.Roles.Where(r => !r.IsDeleted), ur => ur.RoleId, r => r.Id, (_, r) => r.Code)
            .ToList();

        var actorRank = RoleHierarchy.MaxRank(actorRoles);
        var targetRank = RoleHierarchy.RankOf(roleCode);
        if (targetRank >= actorRank && !actorRoles.Contains(RoleCodes.SystemAdministrator, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("لا يمكنك منح دورًا بمستوى يساوي أو أعلى من مستواك.");
        }

        if (targetRank > actorRank)
        {
            throw new UnauthorizedAccessException("لا يمكنك منح دورًا أعلى من دورك.");
        }

        // Actor must hold all permissions of the role being granted.
        var rolePermissions = (
            from r in db.Roles
            join rp in db.RolePermissions on r.Id equals rp.RoleId
            join p in db.Permissions on rp.PermissionId equals p.Id
            where r.Code == roleCode && !r.IsDeleted
            select p.Code).Distinct().ToList();

        foreach (var permission in rolePermissions)
        {
            if (!currentUser.HasPermission(permission) &&
                !actorRoles.Contains(RoleCodes.SystemAdministrator, StringComparer.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"لا يمكنك منح صلاحية لا تمتلكها: {permission}");
            }
        }
    }

    public void EnsureCanAssignScope(Guid targetUserId, ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (!currentUser.HasPermission(PermissionCodes.ScopesManage))
        {
            throw new UnauthorizedAccessException("ليست لديك صلاحية إدارة النطاقات.");
        }

        if (currentUser.UserId == targetUserId)
        {
            throw new UnauthorizedAccessException("لا يمكنك تعديل نطاقاتك بنفسك.");
        }

        ValidateScopeShape(scopeType, regionId, facilityId, facilityUnitId);

        if (scopeType == ScopeType.Global)
        {
            if (!currentUser.HasPermission(PermissionCodes.GrantGlobalScope) && !currentUser.IsGlobalScope)
            {
                throw new UnauthorizedAccessException("منح النطاق الوطني مقصور على مسؤول مصرح له.");
            }

            return;
        }

        if (scopeType == ScopeType.Headquarters)
        {
            if (!currentUser.HasPermission(PermissionCodes.GrantHeadquartersScope) &&
                !currentUser.IsGlobalScope &&
                !currentUser.HasHeadquartersScope)
            {
                throw new UnauthorizedAccessException("منح نطاق المستوى الرئيسي مقصور على مسؤول مصرح له.");
            }

            return;
        }

        if (scopeType is ScopeType.Region or ScopeType.MultipleRegions)
        {
            if (regionId is null || !scopeService.CanAccessRegion(regionId.Value))
            {
                throw new UnauthorizedAccessException("لا يمكنك منح نطاق خارج منطقتك.");
            }
        }

        if (scopeType is ScopeType.Facility or ScopeType.MultipleFacilities or ScopeType.FacilityUnit)
        {
            if (facilityId is null || !scopeService.CanAccessFacility(facilityId.Value))
            {
                throw new UnauthorizedAccessException("لا يمكنك منح نطاق خارج سجنك.");
            }

            if (scopeType == ScopeType.FacilityUnit)
            {
                if (facilityUnitId is null || !scopeService.CanAccessFacilityUnit(facilityUnitId.Value))
                {
                    throw new UnauthorizedAccessException("لا يمكنك منح نطاق وحدة خارج صلاحيتك.");
                }

                var unit = db.FacilityUnits.FirstOrDefault(u => u.Id == facilityUnitId.Value && !u.IsDeleted)
                    ?? throw new InvalidOperationException("الوحدة غير موجودة.");
                if (unit.FacilityId != facilityId.Value)
                {
                    throw new InvalidOperationException("الوحدة لا تتبع السجن المحدد.");
                }
            }
            else
            {
                var facility = db.Facilities.FirstOrDefault(f => f.Id == facilityId.Value && !f.IsDeleted)
                    ?? throw new InvalidOperationException("السجن غير موجود.");
                if (regionId.HasValue && facility.RegionId != regionId.Value)
                {
                    throw new InvalidOperationException("السجن لا يتبع المنطقة المحددة.");
                }
            }
        }
    }

    public static void ValidateScopeShape(ScopeType scopeType, Guid? regionId, Guid? facilityId, Guid? facilityUnitId)
    {
        if (scopeType is ScopeType.Global or ScopeType.Headquarters)
        {
            if (regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
            {
                throw new InvalidOperationException("نطاق Global/Headquarters لا يقبل معرفات منطقة أو سجن أو وحدة.");
            }

            return;
        }

        if (scopeType is ScopeType.Region or ScopeType.MultipleRegions)
        {
            if (!regionId.HasValue || facilityId.HasValue || facilityUnitId.HasValue)
            {
                throw new InvalidOperationException("نطاق المنطقة يتطلب RegionId فقط.");
            }
        }

        if (scopeType is ScopeType.Facility or ScopeType.MultipleFacilities)
        {
            if (!facilityId.HasValue || facilityUnitId.HasValue)
            {
                throw new InvalidOperationException("نطاق السجن يتطلب FacilityId.");
            }
        }

        if (scopeType == ScopeType.FacilityUnit)
        {
            if (!facilityId.HasValue || !facilityUnitId.HasValue)
            {
                throw new InvalidOperationException("نطاق الوحدة يتطلب FacilityId وFacilityUnitId.");
            }
        }
    }
}
