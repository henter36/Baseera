namespace Baseera.Api.Health;

using Baseera.Application.Attachments;
using Baseera.Application.Security;
using Baseera.Infrastructure.Attachments;
using Baseera.Infrastructure.Persistence;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

public static class HealthExtensions
{
    public static IServiceCollection AddBaseeraHealthChecks(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment,
        bool useTestAuth,
        bool seedDemo)
    {
        services.AddSingleton(new RuntimeSecurityState(useTestAuth, seedDemo, environment.EnvironmentName));
        services.AddHealthChecks()
            .AddCheck<SqlReadyHealthCheck>("sql")
            .AddCheck<AttachmentStorageHealthCheck>("attachments")
            .AddCheck<SecurityConfigHealthCheck>("security-config")
            .AddCheck<EntraConfigHealthCheck>("entra-config");

        return services;
    }

    public static IEndpointRouteBuilder MapBaseeraHealthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = _ => false
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            ResponseWriter = async (context, report) =>
            {
                context.Response.ContentType = "application/json";
                var payload = new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.Select(e => new
                    {
                        name = e.Key,
                        status = e.Value.Status.ToString()
                    })
                };
                await context.Response.WriteAsJsonAsync(payload);
            }
        });

        return app;
    }
}

public sealed record RuntimeSecurityState(bool UseTestAuth, bool SeedDemo, string EnvironmentName);

public sealed class SqlReadyHealthCheck(BaseeraDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                return HealthCheckResult.Unhealthy("database_unreachable");
            }

            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any();
            return pending
                ? HealthCheckResult.Degraded("pending_migrations")
                : HealthCheckResult.Healthy();
        }
        catch
        {
            return HealthCheckResult.Unhealthy("database_error");
        }
    }
}

public sealed class AttachmentStorageHealthCheck(IOptions<AttachmentStorageOptions> options) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        string? probe = null;
        try
        {
            var root = StoragePathGuard.NormalizeRoot(options.Value.RootPath);
            Directory.CreateDirectory(root);
            probe = Path.Combine(root, $".health-{Guid.NewGuid():N}");
            StoragePathGuard.EnsureInsideRoot(root, probe);
            var payload = "ok"u8.ToArray();
            await File.WriteAllBytesAsync(probe, payload, cancellationToken);
            var read = await File.ReadAllBytesAsync(probe, cancellationToken);
            if (read.Length != payload.Length)
            {
                return HealthCheckResult.Unhealthy("attachment_storage_io");
            }

            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return HealthCheckResult.Unhealthy("attachment_storage");
        }
        finally
        {
            if (probe is not null && File.Exists(probe))
            {
                File.Delete(probe);
            }
        }
    }
}

public sealed class SecurityConfigHealthCheck(RuntimeSecurityState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (state.UseTestAuth && !EnvironmentSecurityGuard.IsAllowlistedForTestFeatures(state.EnvironmentName))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("testauth_in_restricted_environment"));
        }

        if (state.SeedDemo && !EnvironmentSecurityGuard.IsAllowlistedForTestFeatures(state.EnvironmentName))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("demoseed_in_restricted_environment"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}

public sealed class EntraConfigHealthCheck(IConfiguration configuration, IHostEnvironment environment, RuntimeSecurityState state) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (state.UseTestAuth || EnvironmentSecurityGuard.IsAllowlistedForTestFeatures(environment.EnvironmentName))
        {
            return Task.FromResult(HealthCheckResult.Healthy("test_or_dev_auth_mode"));
        }

        var tenant = configuration["AzureAd:TenantId"];
        var client = configuration["AzureAd:ClientId"];
        if (string.IsNullOrWhiteSpace(tenant) || tenant.Contains("YOUR_", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(client) || client.Contains("YOUR_", StringComparison.Ordinal))
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("entra_not_configured"));
        }

        return Task.FromResult(HealthCheckResult.Healthy());
    }
}
