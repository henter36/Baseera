using System.Security.Claims;
using Baseera.Application.Abstractions;
using Baseera.Application.Security;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Domain.Organization;
using Baseera.Infrastructure.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Baseera.UnitTests;

public sealed class PrivilegeGuardTests
{
    [Fact]
    public void Regional_director_cannot_grant_global_scope()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        SeedActor(db, actorId, RoleCodes.RegionalDirector, [PermissionCodes.ScopesManage],
            new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null));

        var guard = new PrivilegeGuard(
            FakeUser(actorId, [PermissionCodes.ScopesManage], [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]),
            db,
            new OrganizationalScopeService(
                FakeUser(actorId, [PermissionCodes.ScopesManage], [new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null)]),
                db));

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Global, null, null, null));
    }

    [Fact]
    public void Global_without_GrantGlobal_permission_cannot_grant_global()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        SeedActor(db, actorId, RoleCodes.SystemAdministrator, [PermissionCodes.ScopesManage],
            new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var current = FakeUser(actorId, [PermissionCodes.ScopesManage],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));
        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Global, null, null, null));
    }

    [Fact]
    public void GrantGlobal_without_global_scope_fails()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        SeedActor(db, actorId, RoleCodes.SystemAdministrator,
            [PermissionCodes.ScopesManage, PermissionCodes.GrantGlobalScope],
            new UserScopeSnapshot(ScopeType.Headquarters, null, null, null));
        var current = FakeUser(actorId,
            [PermissionCodes.ScopesManage, PermissionCodes.GrantGlobalScope],
            [new UserScopeSnapshot(ScopeType.Headquarters, null, null, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));
        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Global, null, null, null));
    }

    [Fact]
    public void GrantHeadquarters_without_permission_fails()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        SeedActor(db, actorId, RoleCodes.SystemAdministrator, [PermissionCodes.ScopesManage],
            new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var current = FakeUser(actorId, [PermissionCodes.ScopesManage],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));
        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Headquarters, null, null, null));
    }

    [Fact]
    public void Assign_scope_to_missing_user_fails()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        SeedActor(db, actorId, RoleCodes.SystemAdministrator,
            [PermissionCodes.ScopesManage, PermissionCodes.GrantGlobalScope],
            new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var current = FakeUser(actorId,
            [PermissionCodes.ScopesManage, PermissionCodes.GrantGlobalScope],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));
        Assert.Throws<KeyNotFoundException>(() =>
            guard.EnsureCanAssignScope(Guid.NewGuid(), ScopeType.Global, null, null, null));
    }

    [Fact]
    public void Regional_director_cannot_grant_region_b()
    {
        using var db = CreateDb();
        db.Regions.AddRange(
            new Region { Id = SeedIds.RegionA, OrganizationId = SeedIds.Organization, Code = "A", NameAr = "أ" },
            new Region { Id = SeedIds.RegionB, OrganizationId = SeedIds.Organization, Code = "B", NameAr = "ب" });
        db.SaveChanges();

        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        var scopes = new[] { new UserScopeSnapshot(ScopeType.Region, SeedIds.RegionA, null, null) };
        SeedActor(db, actorId, RoleCodes.RegionalDirector, [PermissionCodes.ScopesManage], scopes[0]);
        var current = FakeUser(actorId, [PermissionCodes.ScopesManage], scopes);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Region, SeedIds.RegionB, null, null));
    }

    [Fact]
    public void Facility_director_cannot_grant_other_facility()
    {
        using var db = CreateDb();
        db.Facilities.AddRange(
            new Facility { Id = SeedIds.FacilityA1, RegionId = SeedIds.RegionA, Code = "A1", NameAr = "أ1" },
            new Facility { Id = SeedIds.FacilityB1, RegionId = SeedIds.RegionB, Code = "B1", NameAr = "ب1" });
        db.SaveChanges();

        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        var scopes = new[] { new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null) };
        SeedActor(db, actorId, RoleCodes.FacilityDirector, [PermissionCodes.ScopesManage], scopes[0]);
        var current = FakeUser(actorId, [PermissionCodes.ScopesManage], scopes);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignScope(targetId, ScopeType.Facility, SeedIds.RegionB, SeedIds.FacilityB1, null));
    }

    [Fact]
    public void User_cannot_assign_own_roles()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        SeedActor(db, actorId, RoleCodes.SystemAdministrator,
            [PermissionCodes.RolesManage], new UserScopeSnapshot(ScopeType.Global, null, null, null));
        var current = FakeUser(actorId, [PermissionCodes.RolesManage],
            [new UserScopeSnapshot(ScopeType.Global, null, null, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignRole(actorId, RoleCodes.SystemAdministrator));
    }

    [Fact]
    public void Facility_director_cannot_grant_system_administrator()
    {
        using var db = CreateDb();
        var actorId = Guid.NewGuid();
        var targetId = SeedTarget(db);
        SeedActor(db, actorId, RoleCodes.FacilityDirector, [PermissionCodes.RolesManage],
            new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null));
        var current = FakeUser(actorId, [PermissionCodes.RolesManage],
            [new UserScopeSnapshot(ScopeType.Facility, SeedIds.RegionA, SeedIds.FacilityA1, null)]);
        var guard = new PrivilegeGuard(current, db, new OrganizationalScopeService(current, db));

        Assert.Throws<UnauthorizedAccessException>(() =>
            guard.EnsureCanAssignRole(targetId, RoleCodes.SystemAdministrator));
    }

    [Fact]
    public void Global_hq_scope_rejects_extra_ids()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PrivilegeGuard.ValidateScopeShape(ScopeType.Global, SeedIds.RegionA, null, null));
        Assert.Throws<InvalidOperationException>(() =>
            PrivilegeGuard.ValidateScopeShape(ScopeType.Headquarters, null, SeedIds.FacilityA1, null));
    }

    private static Guid SeedTarget(BaseeraDbContext db)
    {
        var id = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = id,
            ExternalSubject = id.ToString(),
            UserName = id.ToString(),
            DisplayNameAr = "target",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        });
        db.SaveChanges();
        return id;
    }

    private static void SeedActor(
        BaseeraDbContext db,
        Guid userId,
        string roleCode,
        string[] permissions,
        UserScopeSnapshot scope)
    {
        var role = new Role { Id = Guid.NewGuid(), Code = roleCode, NameAr = roleCode, IsSystem = true };
        db.Roles.Add(role);
        db.Users.Add(new User
        {
            Id = userId,
            ExternalSubject = userId.ToString(),
            UserName = userId.ToString(),
            DisplayNameAr = "actor",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        });
        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = role.Id });
        foreach (var code in permissions)
        {
            var permission = new Permission { Id = Guid.NewGuid(), Code = code, NameAr = code, Module = "Test" };
            db.Permissions.Add(permission);
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        }

        db.UserScopes.Add(new UserScope
        {
            UserId = userId,
            ScopeType = scope.ScopeType,
            RegionId = scope.RegionId,
            FacilityId = scope.FacilityId,
            FacilityUnitId = scope.FacilityUnitId,
            IsActive = true
        });
        db.SaveChanges();
    }

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }

    private static ICurrentUser FakeUser(
        Guid userId,
        IReadOnlyCollection<string> permissions,
        IReadOnlyCollection<UserScopeSnapshot> scopes) =>
        new FakeCurrentUser(true, userId, "actor", "actor", permissions, scopes);
}

public sealed class UserProvisioningTests
{
    [Fact]
    public async Task Unknown_subject_is_rejected()
    {
        await using var db = CreateDb();
        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("unknown-subject"));
        Assert.False(state.IsAuthenticated);
        Assert.Equal("not_provisioned", state.RejectionReason);
    }

    [Fact]
    public async Task Pending_user_is_rejected()
    {
        await using var db = CreateDb();
        db.Users.Add(new User
        {
            ExternalSubject = "pending-user",
            UserName = "pending-user",
            DisplayNameAr = "معلق",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Pending
        });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("pending-user"));
        Assert.False(state.IsAuthenticated);
        Assert.Equal("inactive_or_pending", state.RejectionReason);
    }

    [Fact]
    public async Task Inactive_user_is_rejected()
    {
        await using var db = CreateDb();
        db.Users.Add(new User
        {
            ExternalSubject = "inactive-user",
            UserName = "inactive-user",
            DisplayNameAr = "معطل",
            IsActive = false,
            ProvisioningStatus = UserProvisioningStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("inactive-user"));
        Assert.False(state.IsAuthenticated);
        Assert.Equal("inactive_or_pending", state.RejectionReason);
    }

    [Fact]
    public async Task Archived_user_is_rejected()
    {
        await using var db = CreateDb();
        db.Users.Add(new User
        {
            ExternalSubject = "archived-user",
            UserName = "archived-user",
            DisplayNameAr = "مؤرشف",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active,
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("archived-user"));
        Assert.False(state.IsAuthenticated);
        Assert.Equal("archived", state.RejectionReason);
    }

    [Fact]
    public async Task Active_user_without_roles_or_scopes_is_authenticated_empty()
    {
        await using var db = CreateDb();
        db.Users.Add(new User
        {
            ExternalSubject = "empty-user",
            UserName = "empty-user",
            DisplayNameAr = "بلا صلاحيات",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("empty-user"));
        Assert.True(state.IsAuthenticated);
        Assert.Empty(state.Permissions);
        Assert.Empty(state.Scopes);
    }

    [Fact]
    public async Task Archived_role_does_not_grant_permissions()
    {
        await using var db = CreateDb();
        var user = new User
        {
            ExternalSubject = "role-arch",
            UserName = "role-arch",
            DisplayNameAr = "دور مؤرشف",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        };
        var role = new Role
        {
            Code = RoleCodes.FacilityDirector,
            NameAr = "مدير",
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        };
        var permission = new Permission { Code = PermissionCodes.OrganizationView, NameAr = "عرض", Module = "Organization" };
        db.Users.Add(user);
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("role-arch"));
        Assert.True(state.IsAuthenticated);
        Assert.DoesNotContain(PermissionCodes.OrganizationView, state.Permissions);
    }

    [Fact]
    public async Task Archived_scope_is_not_loaded()
    {
        await using var db = CreateDb();
        var user = new User
        {
            ExternalSubject = "scope-arch",
            UserName = "scope-arch",
            DisplayNameAr = "نطاق مؤرشف",
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.UserScopes.Add(new UserScope
        {
            UserId = user.Id,
            ScopeType = ScopeType.Global,
            IsActive = true,
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var service = new UserProvisioningService(db, NullLogger<UserProvisioningService>.Instance);
        var state = await service.ResolveAsync(Principal("scope-arch"));
        Assert.True(state.IsAuthenticated);
        Assert.Empty(state.Scopes);
    }

    private static ClaimsPrincipal Principal(string subject) =>
        new(new ClaimsIdentity(
        [
            new Claim("oid", subject),
            new Claim(ClaimTypes.Name, subject)
        ], "Test"));

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }
}

public sealed class SoftDeleteFilterTests
{
    [Fact]
    public void Archived_facility_is_hidden_by_default()
    {
        using var db = CreateDb();
        db.Facilities.Add(new Facility
        {
            Id = SeedIds.FacilityA1,
            RegionId = SeedIds.RegionA,
            Code = "A1",
            NameAr = "أ1",
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        });
        db.SaveChanges();

        Assert.Empty(db.Facilities.ToList());
        Assert.Single(db.Facilities.IgnoreQueryFilters().ToList());
    }

    [Fact]
    public void Archived_user_is_hidden_by_default()
    {
        using var db = CreateDb();
        db.Users.Add(new User
        {
            ExternalSubject = "x",
            UserName = "x",
            DisplayNameAr = "x",
            IsDeleted = true,
            DeletedAtUtc = DateTimeOffset.UtcNow
        });
        db.SaveChanges();
        Assert.Empty(db.Users.ToList());
    }

    private static BaseeraDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<BaseeraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BaseeraDbContext(options);
    }
}

internal sealed class FakeCurrentUser(
    bool isAuthenticated,
    Guid? userId,
    string? externalSubject,
    string? displayName,
    IReadOnlyCollection<string> permissions,
    IReadOnlyCollection<UserScopeSnapshot> scopes) : ICurrentUser
{
    public bool IsAuthenticated { get; } = isAuthenticated;
    public Guid? UserId { get; } = userId;
    public string? ExternalSubject { get; } = externalSubject;
    public string? DisplayName { get; } = displayName;
    public string? IpAddress => null;
    public string? CorrelationId => "test";
    public IReadOnlyCollection<string> Permissions { get; } = permissions;
    public IReadOnlyCollection<UserScopeSnapshot> Scopes { get; } = scopes;
    public bool IsGlobalScope => Scopes.Any(s => s.ScopeType == ScopeType.Global);
    public bool HasHeadquartersScope => IsGlobalScope || Scopes.Any(s => s.ScopeType == ScopeType.Headquarters);
    public bool HasPermission(string permissionCode) =>
        Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase);
}
