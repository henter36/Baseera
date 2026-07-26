using Baseera.Application.Workforce;
using Baseera.Domain.Identity;
using Baseera.Domain.Workforce;

namespace Baseera.UnitTests.Workforce;

public sealed class WorkforceShiftPolicyTests
{
    [Fact]
    public void ResolveUtcWindow_SameDay_Utc()
    {
        var (start, end) = WorkforceShiftPolicy.ResolveUtcWindow(
            new DateOnly(2026, 7, 25),
            new TimeOnly(8, 0),
            new TimeOnly(16, 0),
            crossesMidnight: false,
            timezoneId: "UTC");

        Assert.Equal(new DateTimeOffset(2026, 7, 25, 8, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(new DateTimeOffset(2026, 7, 25, 16, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void ResolveUtcWindow_CrossesMidnight_Utc()
    {
        var (start, end) = WorkforceShiftPolicy.ResolveUtcWindow(
            new DateOnly(2026, 7, 25),
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            crossesMidnight: true,
            timezoneId: "UTC");

        Assert.Equal(8, (end - start).TotalHours);
        Assert.Equal(new DateTimeOffset(2026, 7, 26, 6, 0, 0, TimeSpan.Zero), end);
    }

    [Fact]
    public void ResolveUtcWindow_DstTimezone_WhenAvailable()
    {
        string? tz = null;
        foreach (var candidate in new[] { "America/New_York", "Eastern Standard Time" })
        {
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(candidate);
                tz = candidate;
                break;
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        if (tz is null)
        {
            return;
        }

        // Around US DST spring forward 2026-03-08.
        var (start, end) = WorkforceShiftPolicy.ResolveUtcWindow(
            new DateOnly(2026, 3, 7),
            new TimeOnly(22, 0),
            new TimeOnly(6, 0),
            crossesMidnight: true,
            timezoneId: tz);

        Assert.True(end > start);
        Assert.True((end - start).TotalHours is >= 7 and <= 9);
    }
}

public sealed class WorkforceAccessRedactionTests
{
    [Fact]
    public void Sensitive_restrictions_require_dedicated_permission()
    {
        Assert.False(WorkforceAccessPolicy.CanViewSensitiveRestrictions([PermissionCodes.WorkforceViewMembers]));
        Assert.True(WorkforceAccessPolicy.CanViewSensitiveRestrictions(
        [
            PermissionCodes.WorkforceViewMembers,
            PermissionCodes.WorkforceViewSensitiveRestrictions
        ]));
    }
}
