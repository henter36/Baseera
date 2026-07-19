using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Baseera.IntegrationTests;

public sealed class BaseeraApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"Baseera_Test_{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            var connection = Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION")
                ?? "Server=127.0.0.1,1433;Database=Baseera_Test_Template;User Id=sa;Password=***REMOVED***;Encrypt=False;TrustServerCertificate=True;MultipleActiveResultSets=true";

            var builderCs = new System.Data.Common.DbConnectionStringBuilder { ConnectionString = connection };
            builderCs["Database"] = _databaseName;

            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Baseera"] = builderCs.ConnectionString,
                ["Auth:UseTestAuth"] = "true",
                ["Seed:DemoOrganization"] = "true",
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

        var user = await db.Users.FirstOrDefaultAsync(u => u.ExternalSubject == subject);
        if (user is null)
        {
            user = new User
            {
                ExternalSubject = subject,
                UserName = subject,
                DisplayNameAr = displayName,
                Email = $"{subject}@test.local",
                IsActive = true
            };
            db.Users.Add(user);
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
