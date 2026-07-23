namespace Baseera.Application.Forms.Campaigns;

using System.Text.Json;
using Baseera.Application.Abstractions;
using Baseera.Domain.Forms;
using Microsoft.EntityFrameworkCore;

public interface IFormTimeZoneResolver
{
    TimeZoneInfo Resolve(string timeZoneId);
    DateTimeOffset ToUtc(DateTimeOffset local, string timeZoneId);
    DateTimeOffset ToLocal(DateTimeOffset utc, string timeZoneId);
    DateTimeOffset NormalizeLocal(DateTimeOffset local, string timeZoneId);
}

public sealed class FormTimeZoneResolver : IFormTimeZoneResolver
{
    public const string DefaultTimeZoneId = "Asia/Riyadh";

    public TimeZoneInfo Resolve(string timeZoneId)
    {
        var id = string.IsNullOrWhiteSpace(timeZoneId) ? DefaultTimeZoneId : timeZoneId.Trim();
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(id);
        }
        catch (TimeZoneNotFoundException)
        {
            throw new InvalidOperationException($"منطقة زمنية غير معروفة: {id}");
        }
        catch (InvalidTimeZoneException)
        {
            throw new InvalidOperationException($"منطقة زمنية غير صالحة: {id}");
        }
    }

    public DateTimeOffset NormalizeLocal(DateTimeOffset local, string timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        var unspecified = DateTime.SpecifyKind(local.DateTime, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(unspecified))
        {
            unspecified = unspecified.AddHours(1);
        }

        var offset = tz.IsAmbiguousTime(unspecified)
            ? tz.GetUtcOffset(unspecified.AddHours(-1))
            : tz.GetUtcOffset(unspecified);
        return new DateTimeOffset(unspecified, offset);
    }

    public DateTimeOffset ToUtc(DateTimeOffset local, string timeZoneId)
    {
        var normalized = NormalizeLocal(local, timeZoneId);
        return normalized.ToUniversalTime();
    }

    public DateTimeOffset ToLocal(DateTimeOffset utc, string timeZoneId)
    {
        var tz = Resolve(timeZoneId);
        var converted = TimeZoneInfo.ConvertTime(utc.UtcDateTime, tz);
        var offset = tz.GetUtcOffset(converted);
        return new DateTimeOffset(DateTime.SpecifyKind(converted, DateTimeKind.Unspecified), offset);
    }
}

public interface IFormRecurrenceCalculator
{
    string BuildOccurrenceKey(Guid campaignId, DateTimeOffset localOccurrence, string timeZoneId);
    IReadOnlyList<DateTimeOffset> EnumerateUpcoming(
        FormCampaignScheduleRequest schedule,
        DateTimeOffset fromLocalInclusive,
        int count);
    (DateTimeOffset OpenAtUtc, DateTimeOffset DueAtUtc, DateTimeOffset GraceEndsAtUtc, DateTimeOffset CloseAtUtc)
        ComputeWindow(DateTimeOffset occurrenceLocal, FormCampaignScheduleRequest schedule, string timeZoneId);
    DateTimeOffset? ComputeNextAfter(FormCampaignScheduleRequest schedule, DateTimeOffset lastLocalInclusive);
    string SerializeSchedule(FormCampaignScheduleRequest schedule);
    FormCampaignScheduleRequest DeserializeSchedule(FormRecurrenceKind kind, string json, DateTimeOffset firstOpenAtLocal, int responseWindowMinutes, int gracePeriodMinutes, int closeAfterMinutes, BusinessDayAdjustment adjustment);
}

public sealed class FormRecurrenceCalculator(IFormTimeZoneResolver timeZones) : IFormRecurrenceCalculator
{
    public const int MaxCustomDates = 100;
    public const int MaxIntervalDays = 365;
    public const int MaxIntervalWeeks = 52;
    public const int MaxIntervalMonths = 24;
    public const int MaxOccurrences = 500;

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public string BuildOccurrenceKey(Guid campaignId, DateTimeOffset localOccurrence, string timeZoneId) =>
        $"{campaignId:N}:{localOccurrence:yyyyMMddHHmm}|{timeZoneId}";

    public IReadOnlyList<DateTimeOffset> EnumerateUpcoming(
        FormCampaignScheduleRequest schedule,
        DateTimeOffset fromLocalInclusive,
        int count)
    {
        if (count <= 0)
        {
            return [];
        }

        return schedule.RecurrenceKind switch
        {
            FormRecurrenceKind.Once => EnumerateOnce(schedule, fromLocalInclusive, count),
            FormRecurrenceKind.Daily => EnumerateDaily(schedule, fromLocalInclusive, count),
            FormRecurrenceKind.Weekly => EnumerateWeekly(schedule, fromLocalInclusive, count),
            FormRecurrenceKind.Monthly => EnumerateMonthly(schedule, fromLocalInclusive, count),
            FormRecurrenceKind.CustomDates => EnumerateCustom(schedule, fromLocalInclusive, count),
            _ => throw new InvalidOperationException("نمط التكرار غير مدعوم.")
        };
    }

    public DateTimeOffset? ComputeNextAfter(FormCampaignScheduleRequest schedule, DateTimeOffset lastLocalInclusive)
    {
        var next = EnumerateUpcoming(schedule, lastLocalInclusive.AddMinutes(1), 1);
        return next.Count == 0 ? null : next[0];
    }

    public (DateTimeOffset OpenAtUtc, DateTimeOffset DueAtUtc, DateTimeOffset GraceEndsAtUtc, DateTimeOffset CloseAtUtc)
        ComputeWindow(DateTimeOffset occurrenceLocal, FormCampaignScheduleRequest schedule, string timeZoneId)
    {
        var openUtc = timeZones.ToUtc(occurrenceLocal, timeZoneId);
        var dueUtc = openUtc.AddMinutes(schedule.ResponseWindowMinutes);
        var graceEndsUtc = dueUtc.AddMinutes(Math.Max(0, schedule.GracePeriodMinutes));
        var closeUtc = graceEndsUtc.AddMinutes(Math.Max(0, schedule.CloseAfterMinutes));
        return (openUtc, dueUtc, graceEndsUtc, closeUtc);
    }

    public string SerializeSchedule(FormCampaignScheduleRequest schedule) =>
        JsonSerializer.Serialize(schedule, JsonOptions);

    public FormCampaignScheduleRequest DeserializeSchedule(
        FormRecurrenceKind kind,
        string json,
        DateTimeOffset firstOpenAtLocal,
        int responseWindowMinutes,
        int gracePeriodMinutes,
        int closeAfterMinutes,
        BusinessDayAdjustment adjustment)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "{}")
        {
            return new FormCampaignScheduleRequest(
                kind, firstOpenAtLocal, responseWindowMinutes, gracePeriodMinutes, closeAfterMinutes,
                adjustment, null, null, null, null, null, null, null, null);
        }

        var parsed = JsonSerializer.Deserialize<FormCampaignScheduleRequest>(json, JsonOptions)
            ?? throw new InvalidOperationException("تعذر قراءة إعدادات التكرار.");
        return parsed with
        {
            RecurrenceKind = kind,
            FirstOpenAtLocal = firstOpenAtLocal,
            ResponseWindowMinutes = responseWindowMinutes,
            GracePeriodMinutes = gracePeriodMinutes,
            CloseAfterMinutes = closeAfterMinutes,
            BusinessDayAdjustment = adjustment
        };
    }

    private static IReadOnlyList<DateTimeOffset> EnumerateOnce(
        FormCampaignScheduleRequest schedule, DateTimeOffset fromLocalInclusive, int count)
    {
        if (schedule.FirstOpenAtLocal < fromLocalInclusive)
        {
            return [];
        }

        return [schedule.FirstOpenAtLocal];
    }

    private static IReadOnlyList<DateTimeOffset> EnumerateDaily(
        FormCampaignScheduleRequest schedule, DateTimeOffset fromLocalInclusive, int count)
    {
        var interval = Math.Clamp(schedule.IntervalDays ?? 1, 1, MaxIntervalDays);
        var results = new List<DateTimeOffset>();
        var cursor = schedule.FirstOpenAtLocal;
        var produced = 0;
        while (results.Count < count && produced < MaxOccurrences)
        {
            produced++;
            if (schedule.UntilLocal is { } until && cursor > until)
            {
                break;
            }

            if (schedule.MaxOccurrences is { } max && produced > max)
            {
                break;
            }

            if (cursor >= fromLocalInclusive)
            {
                results.Add(cursor);
            }

            cursor = cursor.AddDays(interval);
        }

        return results;
    }

    private sealed record OccurrenceEnumerationContext(
        DateTimeOffset FromLocalInclusive,
        DateTimeOffset? UntilLocal,
        int? MaxOccurrenceCount,
        int RequestedCount);

    private enum CandidateDecision
    {
        Include,
        Skip,
        Stop
    }

    private static CandidateDecision EvaluateCandidate(
        DateTimeOffset candidate,
        OccurrenceEnumerationContext context,
        int generatedCount,
        int resultCount)
    {
        if (context.UntilLocal is { } until && candidate > until)
        {
            return CandidateDecision.Stop;
        }

        if (context.MaxOccurrenceCount is { } max && generatedCount > max)
        {
            return CandidateDecision.Stop;
        }

        if (candidate < context.FromLocalInclusive)
        {
            return CandidateDecision.Skip;
        }

        if (resultCount >= context.RequestedCount)
        {
            return CandidateDecision.Stop;
        }

        return CandidateDecision.Include;
    }

    private static IReadOnlyList<DateTimeOffset> EnumerateWeekly(
        FormCampaignScheduleRequest schedule, DateTimeOffset fromLocalInclusive, int count)
    {
        var intervalWeeks = Math.Clamp(schedule.IntervalWeeks ?? 1, 1, MaxIntervalWeeks);
        var days = (schedule.WeekDays is { Count: > 0 } weekDays
            ? weekDays.Distinct().OrderBy(d => d).ToArray()
            : [schedule.FirstOpenAtLocal.DayOfWeek]);
        var timeOfDay = schedule.FirstOpenAtLocal.TimeOfDay;
        var context = new OccurrenceEnumerationContext(fromLocalInclusive, schedule.UntilLocal, schedule.MaxOccurrences, count);
        var results = new List<DateTimeOffset>();
        var weekStart = schedule.FirstOpenAtLocal.Date;
        weekStart = weekStart.AddDays(-(int)weekStart.DayOfWeek);
        var occurrenceIndex = 0;

        for (var week = 0; results.Count < count && occurrenceIndex < MaxOccurrences; week += intervalWeeks)
        {
            foreach (var day in days)
            {
                var date = weekStart.AddDays(week * 7 + (int)day).Add(timeOfDay);
                var local = new DateTimeOffset(date, schedule.FirstOpenAtLocal.Offset);
                if (local < schedule.FirstOpenAtLocal)
                {
                    continue;
                }

                occurrenceIndex++;
                switch (EvaluateCandidate(local, context, occurrenceIndex, results.Count))
                {
                    case CandidateDecision.Stop:
                        return results;
                    case CandidateDecision.Include:
                        results.Add(local);
                        break;
                }
            }
        }

        return results;
    }

    private static IReadOnlyList<DateTimeOffset> EnumerateMonthly(
        FormCampaignScheduleRequest schedule, DateTimeOffset fromLocalInclusive, int count)
    {
        var dayOfMonth = Math.Clamp(schedule.DayOfMonth ?? schedule.FirstOpenAtLocal.Day, 1, 31);
        var policy = schedule.MissingDayPolicy ?? MonthlyMissingDayPolicy.ClampToLastDay;
        var timeOfDay = schedule.FirstOpenAtLocal.TimeOfDay;
        var context = new OccurrenceEnumerationContext(fromLocalInclusive, schedule.UntilLocal, schedule.MaxOccurrences, count);
        var results = new List<DateTimeOffset>();
        var year = schedule.FirstOpenAtLocal.Year;
        var month = schedule.FirstOpenAtLocal.Month;
        var produced = 0;

        while (results.Count < count && produced < MaxOccurrences)
        {
            var daysInMonth = DateTime.DaysInMonth(year, month);
            DateTimeOffset? candidate = null;
            if (dayOfMonth <= daysInMonth)
            {
                candidate = new DateTimeOffset(new DateTime(year, month, dayOfMonth).Add(timeOfDay), schedule.FirstOpenAtLocal.Offset);
            }
            else if (policy == MonthlyMissingDayPolicy.ClampToLastDay)
            {
                candidate = new DateTimeOffset(new DateTime(year, month, daysInMonth).Add(timeOfDay), schedule.FirstOpenAtLocal.Offset);
            }

            if (candidate is { } local && local >= schedule.FirstOpenAtLocal)
            {
                produced++;
                switch (EvaluateCandidate(local, context, produced, results.Count))
                {
                    case CandidateDecision.Stop:
                        return results;
                    case CandidateDecision.Include:
                        results.Add(local);
                        break;
                }
            }

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        return results;
    }

    /// <summary>
    /// Returns custom occurrence dates filtered by schedule bounds.
    /// <see cref="MaxCustomDates"/> applies to the count of distinct dates after <see cref="Enumerable.Distinct"/>.
    /// </summary>
    private static IReadOnlyList<DateTimeOffset> EnumerateCustom(
        FormCampaignScheduleRequest schedule, DateTimeOffset fromLocalInclusive, int count)
    {
        var distinctDates = (schedule.CustomDatesLocal ?? [])
            .Distinct()
            .OrderBy(x => x)
            .ToList();
        if (distinctDates.Count > MaxCustomDates)
        {
            throw new InvalidOperationException(
                $"عدد التواريخ المخصصة يتجاوز الحد الأقصى ({MaxCustomDates}).");
        }

        return distinctDates
            .Where(d => d >= schedule.FirstOpenAtLocal && d >= fromLocalInclusive)
            .Take(count)
            .ToList();
    }
}

public sealed record BusinessCalendarSnapshot(
    Guid OrganizationId,
    DateOnly From,
    DateOnly To,
    IReadOnlyDictionary<DateOnly, bool> WorkingDayOverrides);

public interface IBusinessCalendar
{
    Task<BusinessCalendarSnapshot> LoadAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default);

    DateTimeOffset Adjust(
        DateTimeOffset local,
        BusinessDayAdjustment adjustment,
        BusinessCalendarSnapshot calendar);
}

public sealed class BusinessCalendar(IBaseeraDbContext db) : IBusinessCalendar
{
    private static readonly HashSet<DayOfWeek> Weekend = [DayOfWeek.Friday, DayOfWeek.Saturday];

    public async Task<BusinessCalendarSnapshot> LoadAsync(
        Guid organizationId,
        DateOnly from,
        DateOnly to,
        CancellationToken cancellationToken = default)
    {
        var overrides = await db.OrganizationBusinessCalendarDates
            .AsNoTracking()
            .Where(d => d.OrganizationId == organizationId && d.LocalDate >= from && d.LocalDate <= to)
            .ToDictionaryAsync(d => d.LocalDate, d => d.IsWorkingDayOverride, cancellationToken);

        return new BusinessCalendarSnapshot(organizationId, from, to, overrides);
    }

    public DateTimeOffset Adjust(
        DateTimeOffset local,
        BusinessDayAdjustment adjustment,
        BusinessCalendarSnapshot calendar)
    {
        if (adjustment == BusinessDayAdjustment.None)
        {
            return local;
        }

        var cursor = local;
        for (var i = 0; i < 370; i++)
        {
            var date = DateOnly.FromDateTime(cursor.DateTime);
            var isWeekend = Weekend.Contains(cursor.DayOfWeek);
            var isWorking = calendar.WorkingDayOverrides.TryGetValue(date, out var overrideWorking)
                ? overrideWorking
                : !isWeekend;
            if (isWorking)
            {
                return cursor;
            }

            cursor = adjustment == BusinessDayAdjustment.NextBusinessDay
                ? cursor.AddDays(1)
                : cursor.AddDays(-1);
        }

        throw new InvalidOperationException("تعذر ضبط يوم العمل.");
    }
}
