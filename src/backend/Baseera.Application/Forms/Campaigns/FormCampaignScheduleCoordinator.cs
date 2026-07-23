namespace Baseera.Application.Forms.Campaigns;

using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormCampaignScheduleCoordinator
{
    DateTimeOffset GetUtcNow();
    FormCampaignScheduleRequest MapSchedule(FormCampaign campaign);
    string SerializeSchedule(FormCampaignScheduleRequest schedule);
    void ValidateTimeZone(string? timeZoneId);
    DateTimeOffset ToUtc(DateTimeOffset local, string timeZoneId);
    DateTimeOffset? ComputeNextAfter(FormCampaignScheduleRequest schedule, DateTimeOffset afterLocal, string timeZoneId);
    Task<DateTimeOffset?> ResolveNextOccurrenceForResumeAsync(FormCampaign campaign, CancellationToken cancellationToken = default);
    IReadOnlyList<DateTimeOffset> PreviewUpcoming(FormCampaignScheduleRequest schedule, int count);
    Task<CycleGenerationResult> GenerateOccurrenceAsync(
        FormCampaign campaign,
        DateTimeOffset occurrenceLocal,
        string generatedBy,
        CancellationToken cancellationToken = default);
}

public sealed class FormCampaignScheduleCoordinator(
    IBaseeraDbContext db,
    IFormRecurrenceCalculator recurrence,
    IFormTimeZoneResolver timeZones,
    IFormCycleGenerationService cycleGeneration,
    TimeProvider timeProvider) : IFormCampaignScheduleCoordinator
{
    public DateTimeOffset GetUtcNow() => timeProvider.GetUtcNow();

    public FormCampaignScheduleRequest MapSchedule(FormCampaign campaign) =>
        recurrence.DeserializeSchedule(
            campaign.RecurrenceKind,
            campaign.RecurrenceConfigurationJson,
            campaign.FirstOpenAtLocal,
            campaign.ResponseWindowMinutes,
            campaign.GracePeriodMinutes,
            campaign.CloseAfterMinutes,
            campaign.BusinessDayAdjustment);

    public string SerializeSchedule(FormCampaignScheduleRequest schedule) =>
        recurrence.SerializeSchedule(schedule);

    public void ValidateTimeZone(string? timeZoneId) =>
        _ = timeZones.Resolve(timeZoneId ?? FormTimeZoneResolver.DefaultTimeZoneId);

    public DateTimeOffset ToUtc(DateTimeOffset local, string timeZoneId) =>
        timeZones.ToUtc(local, timeZoneId);

    public DateTimeOffset? ComputeNextAfter(FormCampaignScheduleRequest schedule, DateTimeOffset afterLocal, string timeZoneId)
    {
        var next = recurrence.ComputeNextAfter(schedule, afterLocal);
        return next is null ? null : timeZones.ToUtc(next.Value, timeZoneId);
    }

    public async Task<DateTimeOffset?> ResolveNextOccurrenceForResumeAsync(
        FormCampaign campaign,
        CancellationToken cancellationToken = default)
    {
        var schedule = MapSchedule(campaign);

        if (schedule.RecurrenceKind == FormRecurrenceKind.Once)
        {
            var hasCycle = await db.FormCycles.AsNoTracking()
                .AnyAsync(c => c.CampaignId == campaign.Id, cancellationToken);
            return hasCycle ? null : timeZones.ToUtc(schedule.FirstOpenAtLocal, campaign.TimeZoneId);
        }

        var cursorLocal = campaign.LastGeneratedOccurrenceUtc is { } lastUtc
            ? timeZones.ToLocal(lastUtc, campaign.TimeZoneId).AddMinutes(1)
            : schedule.FirstOpenAtLocal;
        var upcoming = recurrence.EnumerateUpcoming(schedule, cursorLocal, 1);
        return upcoming.Count == 0 ? null : timeZones.ToUtc(upcoming[0], campaign.TimeZoneId);
    }

    public IReadOnlyList<DateTimeOffset> PreviewUpcoming(FormCampaignScheduleRequest schedule, int count) =>
        recurrence.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, Math.Clamp(count, 1, 20));

    public Task<CycleGenerationResult> GenerateOccurrenceAsync(
        FormCampaign campaign,
        DateTimeOffset occurrenceLocal,
        string generatedBy,
        CancellationToken cancellationToken = default) =>
        cycleGeneration.TryGenerateOccurrenceAsync(campaign, occurrenceLocal, generatedBy, cancellationToken);
}
