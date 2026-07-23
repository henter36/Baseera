namespace Baseera.Application.Forms.Campaigns;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public sealed record CycleGenerationResult(FormCycle Cycle, bool Created, int AssignmentsCreated);

public interface IFormCycleGenerationService
{
    Task<CycleGenerationResult> TryGenerateOccurrenceAsync(
        FormCampaign campaign,
        DateTimeOffset occurrenceLocal,
        string generatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class FormCycleGenerationService(
    IBaseeraDbContext db,
    IFormTargetResolver targetResolver,
    IFormRecurrenceCalculator recurrence,
    IFormTimeZoneResolver timeZones,
    IBusinessCalendar businessCalendar,
    IAuditService audit,
    TimeProvider timeProvider) : IFormCycleGenerationService
{
    private const int BusinessDayAdjustmentRangeDays = 370;

    public async Task<CycleGenerationResult> TryGenerateOccurrenceAsync(
        FormCampaign campaign,
        DateTimeOffset occurrenceLocal,
        string generatedBy,
        CancellationToken cancellationToken = default)
    {
        var schedule = recurrence.DeserializeSchedule(
            campaign.RecurrenceKind,
            campaign.RecurrenceConfigurationJson,
            campaign.FirstOpenAtLocal,
            campaign.ResponseWindowMinutes,
            campaign.GracePeriodMinutes,
            campaign.CloseAfterMinutes,
            campaign.BusinessDayAdjustment);

        var occurrenceKey = recurrence.BuildOccurrenceKey(campaign.Id, occurrenceLocal, campaign.TimeZoneId);
        var existing = await db.FormCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampaignId == campaign.Id && c.OccurrenceKey == occurrenceKey, cancellationToken);
        if (existing is not null)
        {
            return new CycleGenerationResult(existing, false, 0);
        }

        BusinessCalendarSnapshot calendar;
        if (campaign.BusinessDayAdjustment == BusinessDayAdjustment.None)
        {
            var date = DateOnly.FromDateTime(occurrenceLocal.DateTime);
            calendar = new BusinessCalendarSnapshot(campaign.OrganizationId, date, date, new Dictionary<DateOnly, bool>());
        }
        else
        {
            var center = DateOnly.FromDateTime(occurrenceLocal.DateTime);
            var from = center.AddDays(-BusinessDayAdjustmentRangeDays);
            var to = center.AddDays(BusinessDayAdjustmentRangeDays);
            calendar = await businessCalendar.LoadAsync(campaign.OrganizationId, from, to, cancellationToken);
        }

        occurrenceLocal = businessCalendar.Adjust(
            occurrenceLocal,
            campaign.BusinessDayAdjustment,
            calendar);

        occurrenceKey = recurrence.BuildOccurrenceKey(campaign.Id, occurrenceLocal, campaign.TimeZoneId);
        existing = await db.FormCycles.AsNoTracking()
            .FirstOrDefaultAsync(c => c.CampaignId == campaign.Id && c.OccurrenceKey == occurrenceKey, cancellationToken);
        if (existing is not null)
        {
            return new CycleGenerationResult(existing, false, 0);
        }

        var targets = campaign.TargetRules
            .Select(r => FormTargetResolver.DeserializeTarget(r.RuleType, r.ConfigurationJson))
            .ToList();
        var exclusions = campaign.Exclusions
            .Select(e => new FormCampaignExclusionRequest(e.FacilityId, e.Reason))
            .ToList();

        var resolution = await targetResolver.ResolveAsync(
            campaign.OrganizationId,
            targets,
            exclusions,
            cancellationToken);

        var window = recurrence.ComputeWindow(occurrenceLocal, schedule, campaign.TimeZoneId);
        var now = timeProvider.GetUtcNow();
        var sequence = await db.FormCycles
            .Where(c => c.CampaignId == campaign.Id)
            .Select(c => (int?)c.SequenceNumber)
            .MaxAsync(cancellationToken) ?? 0;

        var cycle = new FormCycle
        {
            CampaignId = campaign.Id,
            SequenceNumber = sequence + 1,
            OccurrenceKey = occurrenceKey,
            Status = FormCycleStateMachine.ResolveStatus(
                now, window.OpenAtUtc, window.DueAtUtc, window.GraceEndsAtUtc, window.CloseAtUtc, FormCycleStatus.Scheduled),
            ScheduledOccurrenceLocal = occurrenceLocal,
            ScheduledOccurrenceUtc = timeZones.ToUtc(occurrenceLocal, campaign.TimeZoneId),
            OpenAtUtc = window.OpenAtUtc,
            DueAtUtc = window.DueAtUtc,
            GraceEndsAtUtc = window.GraceEndsAtUtc,
            CloseAtUtc = window.CloseAtUtc,
            TimeZoneId = campaign.TimeZoneId,
            FormVersionId = campaign.FormVersionId,
            FormSchemaSnapshotId = campaign.FormSchemaSnapshotId,
            SchemaHash = campaign.SchemaHash,
            TargetSnapshotHash = FormTargetSnapshotHasher.HashAssignments(resolution.Included),
            AssignedFacilityCount = resolution.Included.Count,
            GeneratedAtUtc = now,
            GeneratedBy = generatedBy
        };

        db.Add(cycle);

        foreach (var target in resolution.Included)
        {
            db.Add(new FormFacilityAssignment
            {
                CampaignId = campaign.Id,
                CycleId = cycle.Id,
                FacilityId = target.FacilityId,
                RegionIdAtAssignment = target.RegionId,
                FacilityCodeAtAssignment = target.FacilityCode,
                FacilityNameArAtAssignment = target.FacilityNameAr,
                RegionNameArAtAssignment = target.RegionNameAr,
                FacilityTypeAtAssignment = target.FacilityType,
                TargetRuleType = target.MatchedRuleType,
                AssignedAtUtc = now,
                IsAvailable = target.IsAvailable,
                UnavailableReason = target.UnavailableReason
            });
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (SqlServerUniqueConstraintDetector.IsOccurrenceDuplicate(ex))
        {
            db.ClearChanges();
            var duplicate = await db.FormCycles.AsNoTracking()
                .FirstAsync(c => c.CampaignId == campaign.Id && c.OccurrenceKey == occurrenceKey, cancellationToken);
            return new CycleGenerationResult(duplicate, false, 0);
        }

        await audit.WriteAsync(new AuditEntry
        {
            Action = "FormCycleGenerated",
            Module = FormAccessHelper.ModuleName,
            EntityType = nameof(FormCycle),
            EntityId = cycle.Id.ToString(),
            NewValues = new
            {
                CampaignId = campaign.Id,
                cycle.OccurrenceKey,
                cycle.FormVersionId,
                cycle.SchemaHash,
                cycle.TargetSnapshotHash,
                cycle.AssignedFacilityCount
            }
        }, cancellationToken);

        return new CycleGenerationResult(cycle, true, resolution.Included.Count);
    }
}

internal static class SqlServerUniqueConstraintDetector
{
    private const string OccurrenceUniqueIndex = "IX_FormCycles_CampaignId_OccurrenceKey";

    public static bool IsOccurrenceDuplicate(DbUpdateException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var number = current.GetType().GetProperty("Number")?.GetValue(current);
            if (number is not (2601 or 2627))
            {
                continue;
            }

            if (current.Message.Contains(OccurrenceUniqueIndex, StringComparison.OrdinalIgnoreCase)
                || (current.Message.Contains("CampaignId", StringComparison.OrdinalIgnoreCase)
                    && current.Message.Contains("OccurrenceKey", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
