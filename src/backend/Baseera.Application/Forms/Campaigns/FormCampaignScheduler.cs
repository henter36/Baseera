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
    public async Task<FormCampaignSchedulerRunResult> RunAsync(
        string workerIdentity,
        FormCampaignSchedulerOptions options,
        CancellationToken cancellationToken = default)
    {
        var started = timeProvider.GetUtcNow();
        var dueCampaigns = 0;
        var cyclesCreated = 0;
        var assignmentsCreated = 0;
        var duplicatesSkipped = 0;
        var cyclesStatusUpdated = 0;
        var failures = 0;
        var catchUpLimitReached = false;

        cyclesStatusUpdated += await AdvanceCycleStatusesAsync(cancellationToken);

        var due = await db.FormCampaigns
            .Include(c => c.TargetRules)
            .Include(c => c.Exclusions)
            .Where(c => c.Status == FormCampaignStatus.Scheduled || c.Status == FormCampaignStatus.Active)
            .Where(c => c.NextOccurrenceUtc != null && c.NextOccurrenceUtc <= started)
            .OrderBy(c => c.NextOccurrenceUtc)
            .Take(Math.Max(1, options.BatchSize))
            .ToListAsync(cancellationToken);

        dueCampaigns = due.Count;

        foreach (var campaign in due)
        {
            try
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
                var cursorLocal = campaign.LastGeneratedOccurrenceUtc is { } lastUtc
                    ? timeZones.ToLocal(lastUtc, campaign.TimeZoneId).AddMinutes(1)
                    : schedule.FirstOpenAtLocal;

                while (generatedThisCampaign < Math.Max(1, options.MaxCatchUpOccurrencesPerRun))
                {
                    var upcoming = recurrence.EnumerateUpcoming(schedule, cursorLocal, 1);
                    if (upcoming.Count == 0)
                    {
                        campaign.NextOccurrenceUtc = null;
                        if (await AllCyclesClosedAsync(campaign.Id, cancellationToken))
                        {
                            if (FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Completed))
                            {
                                campaign.Status = FormCampaignStatus.Completed;
                                campaign.ClosedAtUtc = timeProvider.GetUtcNow();
                            }
                        }

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
                        workerIdentity,
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

                    if (campaign.NextOccurrenceUtc is null
                        && await AllCyclesClosedAsync(campaign.Id, cancellationToken)
                        && FormCampaignStateMachine.CanTransition(campaign.Status, FormCampaignStatus.Completed))
                    {
                        campaign.Status = FormCampaignStatus.Completed;
                        campaign.ClosedAtUtc = timeProvider.GetUtcNow();
                    }
                }

                if (generatedThisCampaign >= options.MaxCatchUpOccurrencesPerRun
                    && campaign.NextOccurrenceUtc is { } stillDue
                    && stillDue <= timeProvider.GetUtcNow())
                {
                    catchUpLimitReached = true;
                }

                campaign.UpdatedAtUtc = timeProvider.GetUtcNow();
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                failures++;
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
        }

        return new FormCampaignSchedulerRunResult(
            dueCampaigns,
            cyclesCreated,
            assignmentsCreated,
            duplicatesSkipped,
            cyclesStatusUpdated,
            failures,
            catchUpLimitReached,
            timeProvider.GetUtcNow() - started);
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
