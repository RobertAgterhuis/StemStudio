namespace StemStudio.Domain.Practice;

/// <summary>Local-day buckets for the practice summary: local time = UTC + the browser's offset (-840..840 minutes).</summary>
public static class LocalDay
{
    /// <summary>The local date of a UTC time.</summary>
    /// <param name="utc">UTC time.</param>
    /// <param name="offsetMinutes">Offset from UTC.</param>
    /// <returns>The local date.</returns>
    public static DateOnly DateOf(DateTime utc, int offsetMinutes) => DateOnly.FromDateTime(utc.AddMinutes(offsetMinutes));

    /// <summary>Splits the played time of a session over the local days it touches, in proportion to the wall time per day.</summary>
    /// <param name="startUtc">Session start.</param>
    /// <param name="endUtc">Session end.</param>
    /// <param name="playedSeconds">Played time.</param>
    /// <param name="offsetMinutes">Offset from UTC.</param>
    /// <returns>Seconds per local date, in date order.</returns>
    public static IReadOnlyList<(DateOnly Date, double Seconds)> Split(DateTime startUtc, DateTime endUtc, double playedSeconds, int offsetMinutes)
    {
        var (start, end) = (startUtc.AddMinutes(offsetMinutes), endUtc.AddMinutes(offsetMinutes));
        var span = (end - start).TotalSeconds;
        if (span <= 0)
        {
            return [(DateOnly.FromDateTime(start), playedSeconds)];
        }

        var parts = new List<(DateOnly, double)>();
        for (var cursor = start; cursor < end; cursor = cursor.Date.AddDays(1))
        {
            var dayEnd = cursor.Date.AddDays(1) < end ? cursor.Date.AddDays(1) : end;
            parts.Add((DateOnly.FromDateTime(cursor), playedSeconds * (dayEnd - cursor).TotalSeconds / span));
        }

        return parts;
    }
}
