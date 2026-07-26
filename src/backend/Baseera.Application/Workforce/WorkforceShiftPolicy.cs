namespace Baseera.Application.Workforce;

/// <summary>
/// Resolves local shift windows into UTC, including midnight-crossing shifts, using an explicit timezone id.
/// </summary>
public static class WorkforceShiftPolicy
{
    public static (DateTimeOffset StartUtc, DateTimeOffset EndUtc) ResolveUtcWindow(
        DateOnly dutyDate,
        TimeOnly startLocal,
        TimeOnly endLocal,
        bool crossesMidnight,
        string timezoneId)
    {
        if (string.IsNullOrWhiteSpace(timezoneId))
        {
            throw new ArgumentException("معرّف المنطقة الزمنية مطلوب.", nameof(timezoneId));
        }

        var tz = ResolveTimeZone(timezoneId);
        var startLocalDateTime = dutyDate.ToDateTime(startLocal);
        var endDate = crossesMidnight ? dutyDate.AddDays(1) : dutyDate;
        if (!crossesMidnight && endLocal <= startLocal)
        {
            throw new ArgumentException("نهاية الوردية يجب أن تكون بعد بدايتها ما لم تعبر منتصف الليل.");
        }

        var endLocalDateTime = endDate.ToDateTime(endLocal);
        var startUtc = ToUtcOffset(startLocalDateTime, tz);
        var endUtc = ToUtcOffset(endLocalDateTime, tz);
        if (endUtc <= startUtc)
        {
            throw new ArgumentException("نافذة الوردية بالتوقيت العالمي غير صالحة.");
        }

        return (startUtc, endUtc);
    }

    public static bool IsWithinUtcWindow(DateTimeOffset instantUtc, DateTimeOffset startUtc, DateTimeOffset endUtc) =>
        instantUtc >= startUtc && instantUtc < endUtc;

    private static TimeZoneInfo ResolveTimeZone(string timezoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch (TimeZoneNotFoundException)
        {
            // Linux containers often expose IANA ids; Windows may need conversion.
            if (timezoneId.Equals("Asia/Riyadh", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Arab Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    // fall through
                }
            }

            if (timezoneId.Equals("America/New_York", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");
                }
                catch (TimeZoneNotFoundException)
                {
                    // fall through
                }
            }

            throw new ArgumentException($"المنطقة الزمنية غير معروفة: {timezoneId}", nameof(timezoneId));
        }
        catch (InvalidTimeZoneException ex)
        {
            throw new ArgumentException($"المنطقة الزمنية غير صالحة: {timezoneId}", nameof(timezoneId), ex);
        }
    }

    private static DateTimeOffset ToUtcOffset(DateTime localUnspecified, TimeZoneInfo tz)
    {
        var unspecified = DateTime.SpecifyKind(localUnspecified, DateTimeKind.Unspecified);
        if (tz.IsInvalidTime(unspecified))
        {
            // DST spring-forward gap: nudge forward by one hour.
            unspecified = unspecified.AddHours(1);
        }

        var utc = TimeZoneInfo.ConvertTimeToUtc(unspecified, tz);
        return new DateTimeOffset(utc, TimeSpan.Zero);
    }
}
