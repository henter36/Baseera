using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Baseera.IntegrationTests;

public sealed class BaseeraApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"Baseera_Test_{Guid.NewGuid():N}";
    private readonly string _connectionString;

    public BaseeraApiFactory()
    {
        var raw = Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Fixture must construct even when tests are skipped; no credential fallback.
            _connectionString = "Server=127.0.0.1,1433;Database=Baseera_Skip;Integrated Security=true;Encrypt=False;TrustServerCertificate=True";
            return;
        }

        var builder = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = raw };
        builder["Database"] = _databaseName;
        _connectionString = builder.ConnectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Baseera"] = _connectionString,
                ["Auth:UseTestAuth"] = "true",
                ["Seed:DemoOrganization"] = "true",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Attachments:RootPath"] = Path.Combine(Path.GetTempPath(), "baseera-test-attachments", _databaseName)
            });
        });
    }

    public async Task SeedUserAsync(
        string subject,
        string displayName,
        string[] roleCodes,
        params (ScopeType ScopeType, Guid? RegionId, Guid? FacilityId)[] scopes)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();

        var user = await db.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.ExternalSubject == subject);
        if (user is null)
        {
            user = new User
            {
                ExternalSubject = subject,
                UserName = subject,
                DisplayNameAr = displayName,
                Email = $"{subject}@test.local",
                IsActive = true,
                ProvisioningStatus = UserProvisioningStatus.Active
            };
            db.Users.Add(user);
            await db.SaveChangesAsync();
        }
        else if (user.IsDeleted)
        {
            user.IsDeleted = false;
            user.DeletedAtUtc = null;
            user.IsActive = true;
            user.ProvisioningStatus = UserProvisioningStatus.Active;
            await db.SaveChangesAsync();
        }

        foreach (var roleCode in roleCodes)
        {
            var role = await db.Roles.FirstAsync(r => r.Code == roleCode);
            if (!await db.UserRoles.AnyAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        foreach (var s in scopes)
        {
            db.UserScopes.Add(new UserScope
            {
                UserId = user.Id,
                ScopeType = s.ScopeType,
                RegionId = s.RegionId,
                FacilityId = s.FacilityId,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task MarkAttachmentCleanAsync(Guid attachmentId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var attachment = await db.Attachments.FirstAsync(a => a.Id == attachmentId);
        attachment.ScanStatus = AttachmentScanStatus.Clean;
        await db.SaveChangesAsync();
    }

    public async Task SetUserProvisioningAsync(string subject, bool active, UserProvisioningStatus status)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.ExternalSubject == subject);
        user.IsActive = active;
        user.ProvisioningStatus = status;
        await db.SaveChangesAsync();
    }

    public async Task ArchiveUserAsync(string subject)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var user = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.ExternalSubject == subject);
        user.IsDeleted = true;
        user.DeletedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task SeedUserWithPermissionsAsync(
        string subject,
        string displayName,
        string[] roleCodes,
        string[] extraPermissions,
        params (ScopeType ScopeType, Guid? RegionId, Guid? FacilityId)[] scopes)
    {
        await SeedUserAsync(subject, displayName, roleCodes, scopes);
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
        var user = await db.Users.FirstAsync(u => u.ExternalSubject == subject);
        var role = await db.Roles.FirstAsync(r => r.Code == roleCodes[0]);
        foreach (var code in extraPermissions)
        {
            var permission = await db.Permissions.FirstAsync(p => p.Code == code);
            if (!await db.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id))
            {
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
            }
        }

        await db.SaveChangesAsync();
    }

    public HttpClient CreateAuthenticatedClient(string subject, string? displayName = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-User", subject);
        client.DefaultRequestHeaders.Add("X-Test-DisplayName", displayName ?? subject);
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            try
            {
                using var scope = Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
                db.Database.EnsureDeleted();
            }
            catch
            {
                // best-effort cleanup
            }
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Skips the entire assembly when BASEERA_TEST_CONNECTION is missing.
/// </summary>
public sealed class IntegrationConnectionFactAttribute : FactAttribute
{
    public IntegrationConnectionFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION")))
        {
            Skip = "BASEERA_TEST_CONNECTION is not set.";
        }
    }
}
