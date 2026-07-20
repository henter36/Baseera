namespace Baseera.BackgroundJobs;

using Baseera.Application.Escalations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class BackgroundJobsServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraBackgroundJobs(this IServiceCollection services)
    {
        services.AddOptions<EscalationProcessingOptions>().BindConfiguration("Escalations");
        services.AddHostedService<EscalationProcessingJob>();
        return services;
    }
}

public sealed class EscalationProcessingOptions
{
    public bool Enabled { get; set; }
    public int IntervalSeconds { get; set; } = 300;
    public int BatchSize { get; set; } = 100;
    public int LeaseSeconds { get; set; } = 60;
    public int MaximumAttempts { get; set; } = 3;
    public int RetryBaseSeconds { get; set; } = 60;
}

public sealed class EscalationProcessingJob(
    IServiceScopeFactory scopeFactory,
    IOptions<EscalationProcessingOptions> options,
    TimeProvider timeProvider,
    ILogger<EscalationProcessingJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Escalation processing job is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IEscalationProcessor>();
                var result = await processor.RunAsync(Environment.MachineName, stoppingToken);
                logger.LogInformation(
                    "Escalation processing completed. Policies={Policies} Candidates={Candidates} Occurrences={Occurrences} Notifications={Notifications} Suppressed={Suppressed} Failed={Failed}",
                    result.PoliciesEvaluated,
                    result.CandidatesEvaluated,
                    result.OccurrencesCreated,
                    result.NotificationsCreated,
                    result.Suppressed,
                    result.Failed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Escalation processing run failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, settings.IntervalSeconds)), timeProvider, stoppingToken);
        }
    }
}
