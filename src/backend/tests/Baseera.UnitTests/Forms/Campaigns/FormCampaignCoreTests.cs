using Baseera.Application.Forms.Campaigns;
using Baseera.Domain.Forms;

namespace Baseera.UnitTests.Forms.Campaigns;

public sealed class FormCampaignStateMachineTests
{
    [Theory]
    [InlineData(FormCampaignStatus.Draft, FormCampaignStatus.Scheduled, true)]
    [InlineData(FormCampaignStatus.Draft, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Scheduled, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Active, FormCampaignStatus.Paused, true)]
    [InlineData(FormCampaignStatus.Paused, FormCampaignStatus.Active, true)]
    [InlineData(FormCampaignStatus.Cancelled, FormCampaignStatus.Active, false)]
    [InlineData(FormCampaignStatus.Completed, FormCampaignStatus.Active, false)]
    public void Campaign_transitions(FormCampaignStatus from, FormCampaignStatus to, bool expected) =>
        Assert.Equal(expected, FormCampaignStateMachine.CanTransition(from, to));

    [Fact]
    public void Draft_is_mutable_only()
    {
        Assert.True(FormCampaignStateMachine.IsMutable(FormCampaignStatus.Draft));
        Assert.False(FormCampaignStateMachine.IsMutable(FormCampaignStatus.Active));
    }
}

public sealed class FormRecurrenceCalculatorTests
{
    private readonly FormRecurrenceCalculator _calculator = new(new FormTimeZoneResolver());

    [Fact]
    public void Once_returns_single_occurrence()
    {
        var schedule = BaseSchedule(FormRecurrenceKind.Once, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3)));
        var upcoming = _calculator.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, 5);
        Assert.Single(upcoming);
    }

    [Fact]
    public void Daily_interval_respects_count()
    {
        var schedule = BaseSchedule(FormRecurrenceKind.Daily, new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3))) with
        {
            IntervalDays = 2,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, schedule.FirstOpenAtLocal, 10);
        Assert.Equal(3, upcoming.Count);
        Assert.Equal(2, (upcoming[1] - upcoming[0]).TotalDays);
    }

    [Fact]
    public void Weekly_multiple_weekdays()
    {
        var start = new DateTimeOffset(2026, 7, 6, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Weekly, start) with
        {
            IntervalWeeks = 1,
            WeekDays = [DayOfWeek.Monday, DayOfWeek.Wednesday],
            MaxOccurrences = 4
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 4);
        Assert.Equal(4, upcoming.Count);
        Assert.All(upcoming, d => Assert.True(d.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday));
    }

    [Theory]
    [InlineData(31, 2026, 1, 31)]
    [InlineData(31, 2026, 2, 28)]
    [InlineData(29, 2024, 2, 29)]
    public void Monthly_clamp_to_last_day(int dayOfMonth, int year, int month, int expectedDay)
    {
        var start = new DateTimeOffset(year, month, Math.Min(dayOfMonth, DateTime.DaysInMonth(year, month)), 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Monthly, start) with
        {
            DayOfMonth = dayOfMonth,
            MissingDayPolicy = MonthlyMissingDayPolicy.ClampToLastDay,
            MaxOccurrences = 1
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 1);
        Assert.Single(upcoming);
        Assert.Equal(expectedDay, upcoming[0].Day);
    }

    [Fact]
    public void Monthly_skip_missing_day()
    {
        var start = new DateTimeOffset(2026, 1, 31, 9, 0, 0, TimeSpan.FromHours(3));
        var schedule = BaseSchedule(FormRecurrenceKind.Monthly, start) with
        {
            DayOfMonth = 31,
            MissingDayPolicy = MonthlyMissingDayPolicy.SkipOccurrence,
            MaxOccurrences = 3
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 3);
        Assert.DoesNotContain(upcoming, d => d.Month == 2);
        Assert.All(upcoming, d => Assert.Equal(31, d.Day));
    }

    [Fact]
    public void Custom_dates_unique_sorted()
    {
        var start = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var d2 = start.AddDays(2);
        var d1 = start.AddDays(1);
        var schedule = BaseSchedule(FormRecurrenceKind.CustomDates, start) with
        {
            CustomDatesLocal = [d2, d1, d1, start]
        };
        var upcoming = _calculator.EnumerateUpcoming(schedule, start, 10);
        Assert.Equal([start, d1, d2], upcoming);
    }

    [Fact]
    public void OccurrenceKey_is_deterministic()
    {
        var id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var local = new DateTimeOffset(2026, 7, 1, 9, 30, 0, TimeSpan.FromHours(3));
        var a = _calculator.BuildOccurrenceKey(id, local, "Asia/Riyadh");
        var b = _calculator.BuildOccurrenceKey(id, local, "Asia/Riyadh");
        Assert.Equal(a, b);
        Assert.Contains("Asia/Riyadh", a, StringComparison.Ordinal);
    }

    [Fact]
    public void Asia_Riyadh_conversion_is_stable()
    {
        var resolver = new FormTimeZoneResolver();
        var local = new DateTimeOffset(2026, 7, 1, 9, 0, 0, TimeSpan.FromHours(3));
        var utc = resolver.ToUtc(local, "Asia/Riyadh");
        Assert.Equal(TimeSpan.Zero, utc.Offset);
        Assert.Equal(9, resolver.ToLocal(utc, "Asia/Riyadh").Hour);
    }

    [Fact]
    public void Dst_invalid_time_is_normalized_for_New_York()
    {
        var resolver = new FormTimeZoneResolver();
        var tzId = OperatingSystem.IsWindows() ? "Eastern Standard Time" : "America/New_York";
        var invalid = new DateTimeOffset(2026, 3, 8, 2, 30, 0, TimeSpan.FromHours(-5));
        var normalized = resolver.NormalizeLocal(invalid, tzId);
        Assert.NotEqual(2, normalized.Hour);
    }

    private static FormCampaignScheduleRequest BaseSchedule(FormRecurrenceKind kind, DateTimeOffset first) =>
        new(kind, first, 1440, 60, 0, BusinessDayAdjustment.None, 1, 1, null, null, null, null, null, null);
}
