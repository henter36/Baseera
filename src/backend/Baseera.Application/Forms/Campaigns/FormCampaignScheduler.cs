namespace Baseera.Application.Forms.Campaigns;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public interface IFormCampaignScheduler
{
    Task<FormCampaignSchedulerRunResult> RunAsync(
        string workerIdentity,
        FormCampaignSchedulerOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class FormCampaignScheduler(
    IBaseeraDbContext db,
    IFormCycleGenerationService cycleGeneration,
    IFormRecurrenceCalculator recurrence,
    IFormTimeZoneResolver timeZones,
    IAuditService audit,
    TimeProvider timeProvider,
    ILogger<FormCampaignScheduler> logger) : IFormCampaignScheduler
{
    private sealed record SchedulerExecutionContext(
        string WorkerIdentity,
        DateTimeOffset StartedAtUtc,
        int MaxCatchUpOccurrences);

    private sealed record CampaignProcessingResult(
        int CyclesCreated,
        int AssignmentsCreated,
        int DuplicatesSkipped,
        bool CatchUpLimitReached);

    public async Task<FormCampaignSchedulerRunResult> RunAsync(
        string workerIdentity,
        FormCampaignSchedulerOptions options,
        CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetUtcNow();
        var context = new SchedulerExecutionContext(
            workerIdentity,
            started,
            Math.Max(1, options.MaxCatchUpOccurrencesPerRun));

        var cyclesStatusUpdated = await AdvanceCycleStatusesAsync(cancellationToken);
        var due = await LoadDueCampaignsAsync(started, Math.Max(1, options.BatchSize), cancellationToken);

        var cyclesCreated = 0;
        var assignmentsCreated = 0;
        var duplicatesSkipped = 0;
        var failures = 0;
        var catchUpLimitReached = false;

        foreach (var campaign in due)
        {
            try
            {
                var result = await ProcessCampaignAsync(
                    campaign,
                    context,
                    options.MaxCatchUpOccurrencesPerRun,
                    cancellationToken);
                cyclesCreated += result.CyclesCreated;
                assignmentsCreated += result.AssignmentsCreated;
                duplicatesSkipped += result.DuplicatesSkipped;
                catchUpLimitReached |= result.CatchUpLimitReached;

                campaign.UpdatedAtUtc = timeProvider.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                failures++;
                await HandleCampaignFailureAsync(campaign, ex, cancellationToken);
            }
        }

        return new FormCampaignSchedulerRunResult(
            due.Count,
            cyclesCreated,
            assignmentsCreated,
            duplicatesSkipped,
            cyclesStatusUpdated,
            failures,
            catchUpLimitReached,
            timeProvider.GetUtcNow() - started);
    }

    private async Task<IReadOnlyList<FormCampaign>> LoadDueCampaignsAsync(
        DateTimeOffset startedAtUtc,
        int batchSize,
        CancellationToken cancellationToken) =>
        await db.FormCampaigns
            .Include(c => c.TargetRules)
            .Include(c => c.Exclusions)
            .Where(c => c.Status == FormCampaignStatus.Scheduled || c.Status == FormCampaignStatus.Active)
            .Where(c => c.NextOccurrenceUtc != null && c.NextOccurrenceUtc <= startedAtUtc)
            .OrderBy(c => c.NextOccurrenceUtc)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

    private async Task<CampaignProcessingResult> ProcessCampaignAsync(
        FormCampaign campaign,
        SchedulerExecutionContext context,
        int catchUpLimitThreshold,
        CancellationToken cancellationToken)
    {
        var schedule = recurrence.DeserializeSchedule(
            campaign.RecurrenceKind,
            campaign.RecurrenceConfigurationJson,
            campaign.FirstOpenAtLocal,
            campaign.ResponseWindowMinutes,
            campaign.GracePeriodMinutes,
            campaign.CloseAfterMinutes,
            campaign.BusinessDayAdjustment);

        var generatedThisCampaign = 0;
        var cyclesCreated = 0;
        var assignmentsCreated = 0;
        var duplicatesSkipped = 0;
        var cursorLocal = campaign.LastGeneratedOccurrenceUtc is { } lastUtc
            ? timeZones.ToLocal(lastUtc, campaign.TimeZoneId).AddMinutes(1)
            : schedule.FirstOpenAtLocal;

        while (generatedThisCampaign < context.MaxCatchUpOccurrences)
        {
            var upcoming = recurrence.EnumerateUpcoming(schedule, cursorLocal, 1);
            if (upcoming.Count == 0)
            {
                campaign.NextOccurrenceUtc = null;
                await TryCompleteCampaignIfEligibleAsync(campaign, cancellationToken);
                break;
            }

            var occurrenceLocal = upcoming[0];
            var occurrenceUtc = timeZones.ToUtc(occurrenceLocal, campaign.TimeZoneId);
            if (occurrenceUtc > timeProvider.GetUtcNow())
            {
                campaign.NextOccurrenceUtc = occurrenceUtc;
                break;
            }

            if (campaign.Status == FormCampaignStatus.Scheduled
                && FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Active))
            {
                campaign.Status = FormCampaignStatus.Active;
            }

            var generation = await cycleGeneration.TryGenerateOccurrenceAsync(
                campaign,
                occurrenceLocal,
                context.WorkerIdentity,
                cancellationToken);
            if (generation.Created)
            {
                cyclesCreated++;
                assignmentsCreated += generation.AssignmentsCreated;
            }
            else
            {
                duplicatesSkipped++;
            }

            campaign.LastGeneratedOccurrenceUtc = occurrenceUtc;
            generatedThisCampaign++;
            cursorLocal = occurrenceLocal.AddMinutes(1);

            var next = recurrence.ComputeNextAfter(schedule, occurrenceLocal);
            campaign.NextOccurrenceUtc = next is null ? null : timeZones.ToUtc(next.Value, campaign.TimeZoneId);

            if (campaign.RecurrenceKind == FormRecurrenceKind.Once)
            {
                campaign.NextOccurrenceUtc = null;
                break;
            }

            if (campaign.NextOccurrenceUtc is null)
            {
                await TryCompleteCampaignIfEligibleAsync(campaign, cancellationToken);
            }
        }

        var catchUpLimitReached = generatedThisCampaign >= catchUpLimitThreshold
            && campaign.NextOccurrenceUtc is { } stillDue
            && stillDue <= timeProvider.GetUtcNow();

        return new CampaignProcessingResult(cyclesCreated, assignmentsCreated, duplicatesSkipped, catchUpLimitReached);
    }

    private async Task TryCompleteCampaignIfEligibleAsync(FormCampaign campaign, CancellationToken cancellationToken)
    {
        if (await AllCyclesClosedAsync(campaign.Id, cancellationToken)
            && FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Completed))
        {
            campaign.Status = FormCampaignStatus.Completed;
            campaign.ClosedAtUtc = timeProvider.GetUtcNow();
        }
    }

    private async Task HandleCampaignFailureAsync(
        FormCampaign campaign,
        Exception ex,
        CancellationToken cancellationToken)
    {
        logger.LogError(ex, "Form campaign scheduler failed for {CampaignId}", campaign.Id);
        db.ClearChanges();
        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormSchedulerRunFailed",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCampaign),
            EntityId = campaign.Id.ToString(),
            Outcome = "Failure",
            Reason = ex.Message
        }, cancellationToken);
    }

    private async Task<int> AdvanceCycleStatusesAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var cycles = await db.FormCycles
            .Where(c => c.Status == FormCycleStatus.Scheduled
                || c.Status == FormCycleStatus.Open
                || c.Status == FormCycleStatus.Grace)
            .OrderBy(c => c.OpenAtUtc)
            .Take(500)
            .ToListAsync(cancellationToken);

        var updated = 0;
        foreach (var cycle in cycles)
        {
            var next = FormCycleStateMachine.ResolveStatus(
                now, cycle.OpenAtUtc, cycle.DueAtUtc, cycle.GraceEndsAtUtc, cycle.CloseAtUtc, cycle.Status);
            if (next == cycle.Status)
            {
                continue;
            }

            if (!FormCycleStateMachine.CanTransition(cycle.Status, next)
                && !(cycle.Status == FormCycleStatus.Open && next == FormCycleStatus.Closed))
            {
                continue;
            }

            var from = cycle.Status;
            cycle.Status = next;
            if (next == FormCycleStatus.Closed)
            {
                cycle.ClosedAtUtc = now;
            }

            updated++;
            var action = next switch
            {
                FormCycleStatus.Open => "FormCycleOpened",
                FormCycleStatus.Grace => "FormCycleEnteredGrace",
                FormCycleStatus.Closed => "FormCycleClosed",
                _ => "FormCycleUpdated"
            };
            await audit.WriteAsync(new AuditEntry
            {
                Action = action,
                Module = FormAccessHelper.ModuleName,
                EntityType = nameof(FormCycle),
                EntityId = cycle.Id.ToString(),
                OldValues = new { Status = from },
                NewValues = new { Status = next, cycle.OccurrenceKey }
            }, cancellationToken);
        }

        if (updated > 0)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return updated;
    }

    private Task<bool> AllCyclesClosedAsync(Guid campaignId, CancellationToken cancellationToken) =>
        db.FormCycles.Where(c => c.CampaignId == campaignId)
            .AllAsync(c => c.Status == FormCycleStatus.Closed || c.Status == FormCycleStatus.Cancelled, cancellationToken);
}
