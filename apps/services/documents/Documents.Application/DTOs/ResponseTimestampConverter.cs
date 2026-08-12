namespace Documents.Application.DTOs;

internal static class ResponseTimestampConverter
{
    public static DateTimeOffset Convert(DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value, TimeSpan.Zero),
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc), TimeSpan.Zero),
        };

        return ConvertUtcToPacific(utc);
    }

    public static DateTimeOffset? Convert(DateTime? value)
        => value.HasValue ? Convert(value.Value) : null;

    private static DateTimeOffset ConvertUtcToPacific(DateTimeOffset utc)
    {
        foreach (var timezoneId in new[] { "Pacific Standard Time", "America/Los_Angeles" })
        {
            try
            {
                return TimeZoneInfo.ConvertTime(utc, TimeZoneInfo.FindSystemTimeZoneById(timezoneId));
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        // QA/runtime containers may not have tzdata installed. Fall back to the
        // U.S. Pacific DST rules directly so outbound API timestamps remain PT.
        return utc.ToOffset(IsPacificDaylightSavingTime(utc) ? TimeSpan.FromHours(-7) : TimeSpan.FromHours(-8));
    }

    private static bool IsPacificDaylightSavingTime(DateTimeOffset utc)
    {
        var year = utc.UtcDateTime.Year;
        var dstStartLocal = GetNthSunday(year, month: 3, occurrence: 2).AddHours(2);   // 2:00 AM PST
        var dstEndLocal = GetNthSunday(year, month: 11, occurrence: 1).AddHours(2);    // 2:00 AM PDT

        var dstStartUtc = new DateTimeOffset(dstStartLocal, TimeSpan.FromHours(-8)).ToUniversalTime();
        var dstEndUtc = new DateTimeOffset(dstEndLocal, TimeSpan.FromHours(-7)).ToUniversalTime();

        return utc >= dstStartUtc && utc < dstEndUtc;
    }

    private static DateTime GetNthSunday(int year, int month, int occurrence)
    {
        var date = new DateTime(year, month, 1);
        while (date.DayOfWeek != DayOfWeek.Sunday)
            date = date.AddDays(1);

        return date.AddDays((occurrence - 1) * 7);
    }
}
