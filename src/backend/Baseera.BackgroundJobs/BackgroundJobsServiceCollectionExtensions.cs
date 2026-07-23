namespace Baseera.BackgroundJobs;

using Baseera.Application.Escalations;
using Baseera.Application.Forms.Campaigns;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

public static class BackgroundJobsServiceCollectionExtensions
{
    public static IServiceCollection AddBaseeraBackgroundJobs(this IServiceCollection services)
    {
        services.AddOptions<EscalationProcessingOptions>().BindConfiguration("Escalations");
        services
            .AddOptions<FormCampaignSchedulerHostOptions>()
            .BindConfiguration("FormCampaigns:Scheduler")
            .Validate(x => x.MaxCatchUpOccurrencesPerRun > 0, "MaxCatchUpOccurrencesPerRun must be greater than zero.")
            .Validate(x => x.BatchSize > 0, "BatchSize must be greater than zero.")
            .Validate(x => x.IntervalSeconds > 0, "IntervalSeconds must be greater than zero.")
            .Validate(x => x.MaximumAttempts > 0, "MaximumAttempts must be greater than zero.")
            .Validate(x => x.RetryBaseSeconds > 0, "RetryBaseSeconds must be greater than zero.")
            .ValidateOnStart();
        services.AddHostedService<EscalationProcessingJob>();
        services.AddHostedService<FormCampaignSchedulerService>();
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

public sealed class FormCampaignSchedulerHostOptions
{
    public bool Enabled { get; set; } = true;
    public int IntervalSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public int MaxCatchUpOccurrencesPerRun { get; set; } = 10;
    public int MaximumAttempts { get; set; } = 3;
    public int RetryBaseSeconds { get; set; } = 30;
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
                var result = await processor.RunAsync(
                    Environment.MachineName,
                    new EscalationRunOptions(
                        settings.BatchSize,
                        settings.LeaseSeconds,
                        settings.MaximumAttempts,
                        settings.RetryBaseSeconds),
                    stoppingToken);
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

public sealed class FormCampaignSchedulerService(
    IServiceScopeFactory scopeFactory,
    IOptions<FormCampaignSchedulerHostOptions> options,
    TimeProvider timeProvider,
    ILogger<FormCampaignSchedulerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            logger.LogInformation("Form campaign scheduler is disabled.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var scheduler = scope.ServiceProvider.GetRequiredService<IFormCampaignScheduler>();
                logger.LogInformation("Form campaign scheduler run started. Correlation={Correlation}", Guid.NewGuid());
                var result = await scheduler.RunAsync(
                    Environment.MachineName,
                    new FormCampaignSchedulerOptions(
                        settings.BatchSize,
                        settings.MaxCatchUpOccurrencesPerRun,
                        settings.MaximumAttempts,
                        settings.RetryBaseSeconds),
                    stoppingToken);
                logger.LogInformation(
                    "Form campaign scheduler completed. Due={Due} Cycles={Cycles} Assignments={Assignments} DuplicatesSkipped={Duplicates} StatusUpdated={StatusUpdated} Failures={Failures} CatchUpLimit={CatchUp} DurationMs={Duration}",
                    result.DueCampaigns,
                    result.CyclesCreated,
                    result.AssignmentsCreated,
                    result.DuplicatesSkipped,
                    result.CyclesStatusUpdated,
                    result.Failures,
                    result.CatchUpLimitReached,
                    result.Duration.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Form campaign scheduler run failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(15, settings.IntervalSeconds)), timeProvider, stoppingToken);
        }
    }
}
