using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class IdentityNavigationIntegrationTests : IClassFixture<BaseeraApiFactory>
{
    private readonly BaseeraApiFactory _factory;

    public IdentityNavigationIntegrationTests(BaseeraApiFactory factory) => _factory = factory;

    [IntegrationConnectionFact]
    public async Task Identity_navigation_properties_materialize_and_translate()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var roleCode = $"IdentityNavigationRole-{suffix}";
        var permissionCode = $"Identity.Navigation.Permission.{suffix}";
        var subject = $"identity-navigation-{suffix}";
        const string displayName = "مستخدم اختبار العلاقات";

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        var user = new User
        {
            ExternalSubject = subject,
            UserName = subject,
            DisplayNameAr = displayName,
            IsActive = true,
            ProvisioningStatus = UserProvisioningStatus.Active
        };
        var role = new Role
        {
            Code = roleCode,
            NameAr = "دور اختبار العلاقات"
        };
        var permission = new Permission
        {
            Code = permissionCode,
            NameAr = "صلاحية اختبار العلاقات",
            Module = "Identity"
        };

        db.Users.Add(user);
        db.Roles.Add(role);
        db.Permissions.Add(permission);
        await db.SaveChangesAsync();

        db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
        db.UserScopes.Add(new UserScope
        {
            UserId = user.Id,
            ScopeType = ScopeType.Global,
            IsActive = true
        });
        await db.SaveChangesAsync();

        db.ChangeTracker.Clear();

        var rolePermission = await db.RolePermissions
            .Include(x => x.Role)
            .Include(x => x.Permission)
            .SingleAsync(x => x.RoleId == role.Id && x.PermissionId == permission.Id);
        Assert.Equal(roleCode, rolePermission.Role.Code);
        Assert.Equal(permissionCode, rolePermission.Permission.Code);

        var translatedUserRole = await db.UserRoles
            .Where(x => x.Role.Code == roleCode)
            .Select(x => new { x.UserId, x.Role.Code })
            .SingleAsync();
        Assert.Equal(user.Id, translatedUserRole.UserId);
        Assert.Equal(roleCode, translatedUserRole.Code);

        var includedUserRole = await db.UserRoles
            .Include(x => x.Role)
            .SingleAsync(x => x.UserId == user.Id && x.RoleId == role.Id);
        Assert.Equal(roleCode, includedUserRole.Role.Code);

        var includedScope = await db.UserScopes
            .Include(x => x.User)
            .SingleAsync(x => x.UserId == user.Id);
        Assert.Equal(displayName, includedScope.User.DisplayNameAr);
    }
}
