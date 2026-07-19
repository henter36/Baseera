namespace Baseera.Infrastructure.DependencyInjection;

using Baseera.Application.Abstractions;
using Baseera.Infrastructure.Attachments;
using Baseera.Infrastructure.Audit;
using Baseera.Infrastructure.Identity;
using Baseera.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Baseera")
            ?? throw new InvalidOperationException("Connection string 'Baseera' is missing.");

        services.AddDbContext<BaseeraDbContext>(options =>
        {
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure(3));
            options.AddInterceptors(new AuditImmutabilityInterceptor());
        });

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
}
