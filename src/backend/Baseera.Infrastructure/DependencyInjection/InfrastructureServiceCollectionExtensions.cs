namespace Baseera.Infrastructure.DependencyInjection;

using Baseera.Application.Abstractions;
using Baseera.Infrastructure.Attachments;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Identity;
using Baseera.Infrastructure.Persistence;
using Baseera.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = configuration.GetConnectionString("Baseera")
            ?? throw new InvalidOperationException("Connection string 'Baseera' is missing.");

        services.AddDbContext<BaseeraDbContext>((sp, options) =>
        {
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
            options.AddInterceptors(new AuditImmutabilityInterceptor());
            options.AddInterceptors(sp.GetServices<Microsoft.EntityFrameworkCore.Diagnostics.IInterceptor>());
        });

        ConfigureDataProtection(services, configuration, environment);
        services.AddSingleton<ISensitiveValueProtector, DataProtectionSensitiveValueProtector>();

        services.AddScoped<IBaseeraDbContext>(sp => sp.GetRequiredService<BaseeraDbContext>());
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddScoped<ICurrentUser>(sp => sp.GetRequiredService<CurrentUser>());
        services.AddScoped<UserProvisioningService>();
        services.AddScoped<IAuditService, AuditService>();
        services.Configure<AttachmentStorageOptions>(configuration.GetSection("Attachments"));
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IAttachmentService, AttachmentService>();

        return services;
    }

    internal static void ConfigureDataProtection(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var keysPath = ResolveDataProtectionKeysPath(configuration, environment);
        Directory.CreateDirectory(keysPath);
        EnsureKeysPathWritable(keysPath);

        services.Configure<DataProtectionKeyRingOptions>(options => options.KeysPath = keysPath);
        services.AddHostedService<DataProtectionKeyRingStartupLogger>();

        services
            .AddDataProtection()
            .SetApplicationName("Baseera")
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath));
    }

    internal static string ResolveDataProtectionKeysPath(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var configuredPath = configuration["DataProtection:KeysPath"];

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            if (!Path.IsPathFullyQualified(configuredPath))
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath must be an absolute path.");
            }

            return configuredPath;
        }

        if (!environment.IsDevelopment()
            && !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "DataProtection:KeysPath is required in restricted environments and must point to a durable shared key ring.");
        }

        return Path.Combine(
            Path.GetTempPath(),
            "baseera-data-protection-keys",
            environment.EnvironmentName);
    }

    internal static void EnsureKeysPathWritable(string keysPath)
    {
        var probePath = Path.Combine(keysPath, $".write-probe-{Guid.NewGuid():N}");
        try
        {
            using var stream = new FileStream(
                probePath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.DeleteOnClose);
            stream.WriteByte(1);
            stream.Flush();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new InvalidOperationException(
                $"DataProtection:KeysPath '{keysPath}' is not writable by the application process.",
                ex);
        }
    }
}

internal sealed class DataProtectionKeyRingOptions
{
    public string KeysPath { get; set; } = string.Empty;
}

internal sealed class DataProtectionKeyRingStartupLogger(
    ILogger<DataProtectionKeyRingStartupLogger> logger,
    IOptions<DataProtectionKeyRingOptions> options) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "ASP.NET Data Protection key ring path configured at {KeysPath}. Use a persistent shared volume for all application replicas.",
            options.Value.KeysPath);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
