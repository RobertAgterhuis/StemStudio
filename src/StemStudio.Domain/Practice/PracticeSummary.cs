namespace StemStudio.Domain.Practice;

/// <summary>The window of a summary: now, the number of local days shown and the browser's offset.</summary>
public sealed record SummaryWindow
{
    /// <summary>Creates a window.</summary>
    /// <param name="nowUtc">Now.</param>
    /// <param name="days">Local days in <see cref="PracticeSummary.Days"/> (1..365).</param>
    /// <param name="offsetMinutes">Offset from UTC (-840..840).</param>
    public SummaryWindow(DateTime nowUtc, int days, int offsetMinutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(days, 365);
        ArgumentOutOfRangeException.ThrowIfLessThan(offsetMinutes, -840);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offsetMinutes, 840);
        (NowUtc, Days, OffsetMinutes) = (nowUtc, days, offsetMinutes);
    }

    /// <summary>Now (UTC).</summary>
    public DateTime NowUtc { get; }

    /// <summary>Local days shown.</summary>
    public int Days { get; }

    /// <summary>Offset from UTC in minutes.</summary>
    public int OffsetMinutes { get; }

    /// <summary>Today's local date.</summary>
    public DateOnly Today => LocalDay.DateOf(NowUtc, OffsetMinutes);
}

/// <summary>Seconds on one local day.</summary>
/// <param name="Date">Local date.</param>
/// <param name="Seconds">Played seconds.</param>
public sealed record PracticeDay(DateOnly Date, double Seconds);

/// <summary>The highest trainer speed reached on one day for one section.</summary>
/// <param name="Date">Local date.</param>
/// <param name="SectionId">Section id, or null.</param>
/// <param name="Label">Label.</param>
/// <param name="Percent">Speed percent.</param>
public sealed record PracticeTempoPoint(DateOnly Date, Guid? SectionId, string Label, double Percent);

/// <summary>All practice of one section of a track.</summary>
/// <param name="SectionId">Section id, or null (grouped by label and kind).</param>
/// <param name="Kind">Kind (latest snapshot).</param>
/// <param name="Label">Current label when the section still exists, else the latest snapshot.</param>
/// <param name="Seconds">Played seconds.</param>
/// <param name="LoopPasses">Loop passes.</param>
/// <param name="BestTrainerPercent">Highest trainer speed reached, or null.</param>
/// <param name="LastPractisedAt">End of the latest session with this section.</param>
public sealed record PracticeSectionTotal(Guid? SectionId, PracticeSectionKind Kind, string Label, double Seconds, int LoopPasses, double? BestTrainerPercent, DateTime LastPractisedAt);

/// <summary>Totals of one track for the Practice page.</summary>
/// <param name="TrackId">The track.</param>
/// <param name="TotalSeconds">All played time.</param>
/// <param name="Last7DaysSeconds">Played time in the last 7 local days.</param>
/// <param name="Sessions">Session count.</param>
/// <param name="LastPractisedAt">End of the latest session.</param>
public sealed record PracticeTrackTotal(Guid TrackId, double TotalSeconds, double Last7DaysSeconds, int Sessions, DateTime LastPractisedAt);

/// <summary>The practice summary of one track (data-model 3.8).</summary>
public sealed record PracticeSummary
{
    /// <summary>Played today (local).</summary>
    public required double TodaySeconds { get; init; }

    /// <summary>Played in the last 7 local days (today included).</summary>
    public required double Last7DaysSeconds { get; init; }

    /// <summary>All played time.</summary>
    public required double TotalSeconds { get; init; }

    /// <summary>Session count.</summary>
    public required int Sessions { get; init; }

    /// <summary>End of the latest session, or null.</summary>
    public required DateTime? LastPractisedAt { get; init; }

    /// <summary>One entry per local day of the window, oldest first.</summary>
    public required IReadOnlyList<PracticeDay> Days { get; init; }

    /// <summary>Per section, most practised first.</summary>
    public required IReadOnlyList<PracticeSectionTotal> Sections { get; init; }

    /// <summary>Tempo reached per day and section in the window.</summary>
    public required IReadOnlyList<PracticeTempoPoint> Tempo { get; init; }
}
