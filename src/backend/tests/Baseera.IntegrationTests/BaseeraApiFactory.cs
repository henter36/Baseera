using Baseera.Domain.Attachments;
using Baseera.Domain.Common;
using Baseera.Domain.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Baseera.IntegrationTests;

public sealed class BaseeraApiFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;
    private readonly string _databaseName;
    private readonly bool _applyMigrationsOnStartup;
    private readonly bool _seedDemoOrganization;
    private readonly IInterceptor? _interceptor;
    private readonly string _attachmentsPath;
    private readonly string _dataProtectionKeysPath;
    private readonly bool _ownsTemporaryDirectories;
    private int _disposed;

    public string AttachmentsPath => _attachmentsPath;

    public string DataProtectionKeysPath => _dataProtectionKeysPath;

    public BaseeraApiFactory()
        : this(CreateIsolatedConnectionString(), applyMigrationsOnStartup: true, seedDemoOrganization: true, interceptor: null)
    {
    }

    public BaseeraApiFactory(
        string connectionString,
        bool applyMigrationsOnStartup = false,
        bool seedDemoOrganization = false)
        : this(connectionString, applyMigrationsOnStartup, seedDemoOrganization, interceptor: null)
    {
    }

    public BaseeraApiFactory(
        string connectionString,
        bool applyMigrationsOnStartup,
        bool seedDemoOrganization,
        string? dataProtectionKeysPath,
        bool? ownsTemporaryDirectories = null)
        : this(
            connectionString,
            applyMigrationsOnStartup,
            seedDemoOrganization,
            interceptor: null,
            dataProtectionKeysPath,
            ownsTemporaryDirectories)
    {
    }

    private BaseeraApiFactory(
        string connectionString,
        bool applyMigrationsOnStartup,
        bool seedDemoOrganization,
        IInterceptor? interceptor,
        string? dataProtectionKeysPath = null,
        bool? ownsTemporaryDirectories = null)
    {
        _connectionString = connectionString;
        _databaseName = DatabaseNameFrom(connectionString);
        _applyMigrationsOnStartup = applyMigrationsOnStartup;
        _seedDemoOrganization = seedDemoOrganization;
        _interceptor = interceptor;
        _attachmentsPath = Path.Combine(
            Path.GetTempPath(),
            "baseera-test-attachments",
            _databaseName);
        _dataProtectionKeysPath = dataProtectionKeysPath
            ?? Path.Combine(
                Path.GetTempPath(),
                "baseera-test-dp-keys",
                _databaseName);
        // Unique attachment roots are always owned by this factory. A caller-supplied key
        // ring path is treated as not owned unless ownership is explicitly requested, so
        // omitting the flag can never delete a shared key ring.
        _ownsTemporaryDirectories = ownsTemporaryDirectories ?? dataProtectionKeysPath is null;
    }

    private static string CreateIsolatedConnectionString()
    {
        var raw = Environment.GetEnvironmentVariable("BASEERA_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Fixture must construct even when tests are skipped; no credential fallback.
            return "Server=127.0.0.1,1433;Database=Baseera_Skip;Integrated Security=true;Encrypt=False;TrustServerCertificate=True";
        }

        var builder = new SqlConnectionStringBuilder(raw)
        {
            InitialCatalog = $"Baseera_Test_{Guid.NewGuid():N}"
        };
        return builder.ConnectionString;
    }

    private static string DatabaseNameFrom(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        return string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "Baseera_Test" : builder.InitialCatalog;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting is reliable with WebApplicationFactory + minimal hosting;
        // ConfigureAppConfiguration alone can be ignored by WebApplication.CreateBuilder.
        builder.UseSetting("ConnectionStrings:Baseera", _connectionString);
        builder.UseSetting("Auth:UseTestAuth", "true");
        builder.UseSetting("Seed:DemoOrganization", _seedDemoOrganization.ToString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", _applyMigrationsOnStartup.ToString());
        builder.UseSetting("Attachments:RootPath", _attachmentsPath);
        builder.UseSetting("DataProtection:KeysPath", _dataProtectionKeysPath);
        ConfigureTestLogging(builder);

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Baseera"] = _connectionString,
                ["Auth:UseTestAuth"] = "true",
                ["Seed:DemoOrganization"] = _seedDemoOrganization.ToString(),
                ["Database:ApplyMigrationsOnStartup"] = _applyMigrationsOnStartup.ToString(),
                ["Attachments:RootPath"] = _attachmentsPath,
                ["DataProtection:KeysPath"] = _dataProtectionKeysPath
            });
        });

        if (_interceptor is not null)
        {
            builder.ConfigureServices(services => services.AddSingleton(_interceptor));
        }
    }

    private static void ConfigureTestLogging(IWebHostBuilder builder)
    {
        var verboseSql = string.Equals(
            Environment.GetEnvironmentVariable("BASEERA_TEST_SQL_LOGGING"),
            "verbose",
            StringComparison.OrdinalIgnoreCase);

        builder.UseSetting("Logging:LogLevel:Default", "Warning");
        builder.UseSetting("Logging:LogLevel:Microsoft.EntityFrameworkCore", "Warning");
        builder.UseSetting(
            "Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command",
            verboseSql ? "Information" : "Warning");
    }

    public static BaseeraApiFactory WithInterceptor(IInterceptor interceptor) =>
        new(
            CreateIsolatedConnectionString(),
            applyMigrationsOnStartup: true,
            seedDemoOrganization: true,
            interceptor);

    public static BaseeraApiFactory CreateIsolated(
        string? dataProtectionKeysPath = null,
        bool? ownsTemporaryDirectories = null) =>
        new(
            CreateIsolatedConnectionString(),
            applyMigrationsOnStartup: true,
            seedDemoOrganization: true,
            interceptor: null,
            dataProtectionKeysPath,
            ownsTemporaryDirectories);

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
        }
        else if (user.IsDeleted)
        {
            user.IsDeleted = false;
            user.DeletedAtUtc = null;
            user.IsActive = true;
            user.ProvisioningStatus = UserProvisioningStatus.Active;
        }

        var distinctRoleCodes = roleCodes.Distinct(StringComparer.Ordinal).ToArray();
        var roles = await db.Roles
            .Where(role => distinctRoleCodes.Contains(role.Code))
            .ToDictionaryAsync(role => role.Code);

        var missingRole = distinctRoleCodes.FirstOrDefault(roleCode => !roles.ContainsKey(roleCode));
        if (missingRole is not null)
        {
            throw new InvalidOperationException($"Role '{missingRole}' was not found.");
        }

        var requestedRoleIds = roles.Values.Select(role => role.Id).ToArray();
        var existingRoleIds = requestedRoleIds.Length == 0
            ? []
            : await db.UserRoles
                .Where(userRole => userRole.UserId == user.Id && requestedRoleIds.Contains(userRole.RoleId))
                .Select(userRole => userRole.RoleId)
                .ToListAsync();
        var existingRoleSet = existingRoleIds.ToHashSet();

        foreach (var role in roles.Values)
        {
            if (!existingRoleSet.Contains(role.Id))
            {
                db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = role.Id });
            }
        }

        var existingScopes = await db.UserScopes
            .Where(userScope => userScope.UserId == user.Id && userScope.FacilityUnitId == null)
            .Select(userScope => new
            {
                userScope.ScopeType,
                userScope.RegionId,
                userScope.FacilityId
            })
            .ToListAsync();

        foreach (var s in scopes)
        {
            if (existingScopes.Any(userScope =>
                userScope.ScopeType == s.ScopeType &&
                userScope.RegionId == s.RegionId &&
                userScope.FacilityId == s.FacilityId))
            {
                continue;
            }

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
        if (!disposing)
        {
            base.Dispose(false);
            return;
        }

        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        try
        {
            DeleteDatabaseBestEffort();
        }
        finally
        {
            base.Dispose(true);
            // Attachments are always unique per factory/database and safe to remove.
            DeleteDirectoryBestEffort(_attachmentsPath);
            if (_ownsTemporaryDirectories)
            {
                DeleteDirectoryBestEffort(_dataProtectionKeysPath);
            }
        }
    }

    private void DeleteDatabaseBestEffort()
    {
        try
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<BaseeraDbContext>();
            SqlConnection.ClearAllPools();
            db.Database.EnsureDeleted();
        }
        catch (ObjectDisposedException)
        {
            // Host already torn down.
        }
        catch (InvalidOperationException)
        {
            // Services unavailable after partial disposal.
        }
        catch (SqlException)
        {
            // Best-effort SQL cleanup.
        }
    }

    private static void DeleteDirectoryBestEffort(
        string path,
        ILogger? logger = null)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (IOException ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to delete integration test directory {Path}.",
                path);
        }
        catch (UnauthorizedAccessException ex)
        {
            logger?.LogWarning(
                ex,
                "Failed to delete integration test directory {Path}.",
                path);
        }
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
