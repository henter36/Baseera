namespace Baseera.BackgroundJobs;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public static class BackgroundJobsServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraBackgroundJobs(this IServiceCollection services)
    {
        services.AddHostedService<EscalationPlaceholderJob>();
        return services;
    }
}

/// <summary>
/// Placeholder hosted service for future escalation/notification schedules (phases B+).
/// </summary>
public sealed class EscalationPlaceholderJob(ILogger<EscalationPlaceholderJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Baseera background jobs host started (Phase A placeholder).");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
