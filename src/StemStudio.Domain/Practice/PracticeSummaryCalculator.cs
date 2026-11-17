namespace StemStudio.Domain.Practice;

/// <summary>Computes the practice summary and the overview on read (014 FR-027, data-model 3.8).</summary>
public static class PracticeSummaryCalculator
{
    private const int WeekDays = 7;

    /// <summary>The summary of one track.</summary>
    /// <param name="sessions">All sessions of the track.</param>
    /// <param name="window">Now, days and offset.</param>
    /// <param name="currentLabels">Current label per existing section id.</param>
    /// <returns>The summary.</returns>
    public static PracticeSummary Summarize(IReadOnlyCollection<PracticeSession> sessions, SummaryWindow window, IReadOnlyDictionary<Guid, string> currentLabels)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(window);
        var perDay = SecondsPerDay(sessions, window.OffsetMinutes);
        var sections = SectionTotals(sessions, currentLabels);
        return new PracticeSummary
        {
            TodaySeconds = perDay.GetValueOrDefault(window.Today),
            Last7DaysSeconds = SumSince(perDay, window.Today.AddDays(1 - WeekDays)),
            TotalSeconds = sessions.Sum(session => session.PlayedSeconds),
            Sessions = sessions.Count,
            LastPractisedAt = sessions.Count == 0 ? null : sessions.Max(session => session.EndedAt),
            Days = Enumerable.Range(0, window.Days).Select(i => window.Today.AddDays(i + 1 - window.Days))
                .Select(date => new PracticeDay(date, perDay.GetValueOrDefault(date))).ToList(),
            Sections = sections.Select(group => group.Total).ToList(),
            Tempo = TempoPoints(sections, window),
        };
    }

    /// <summary>Totals per track, most recently practised first.</summary>
    /// <param name="sessions">All sessions.</param>
    /// <param name="window">Now and offset (the days are not used).</param>
    /// <returns>One total per track with sessions.</returns>
    public static IReadOnlyList<PracticeTrackTotal> Overview(IReadOnlyCollection<PracticeSession> sessions, SummaryWindow window)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(window);
        var weekStart = window.Today.AddDays(1 - WeekDays);
        return sessions.GroupBy(session => session.TrackId)
            .Select(track => new PracticeTrackTotal(
                track.Key,
                track.Sum(session => session.PlayedSeconds),
                SumSince(SecondsPerDay(track.ToList(), window.OffsetMinutes), weekStart),
                track.Count(),
                track.Max(session => session.EndedAt)))
            .OrderByDescending(total => total.LastPractisedAt).ToList();
    }

    private static Dictionary<DateOnly, double> SecondsPerDay(IEnumerable<PracticeSession> sessions, int offsetMinutes)
    {
        var perDay = new Dictionary<DateOnly, double>();
        foreach (var (date, seconds) in sessions.SelectMany(session => LocalDay.Split(session.StartedAt, session.EndedAt, session.PlayedSeconds, offsetMinutes)))
        {
            perDay[date] = perDay.GetValueOrDefault(date) + seconds;
        }

        return perDay;
    }

    private static double SumSince(Dictionary<DateOnly, double> perDay, DateOnly first) =>
        perDay.Where(day => day.Key >= first).Sum(day => day.Value);

    private static List<SectionGroup> SectionTotals(IEnumerable<PracticeSession> sessions, IReadOnlyDictionary<Guid, string> currentLabels) =>
        sessions.SelectMany(session => session.Sections.Select(row => (Session: session, Row: row)))
            .GroupBy(item => GroupKey(item.Row))
            .Select(group => SectionGroup.From(group.ToList(), currentLabels))
            .OrderByDescending(group => group.Total.Seconds).ToList();

    private static (Guid?, string, PracticeSectionKind) GroupKey(PracticeSectionTime row) =>
        row.SectionId is { } id ? (id, string.Empty, PracticeSectionKind.None) : (null, row.Label, row.Kind);

    private static List<PracticeTempoPoint> TempoPoints(List<SectionGroup> sections, SummaryWindow window)
    {
        var first = window.Today.AddDays(1 - window.Days);
        return sections.SelectMany(group => group.Rows
                .Where(item => item.Row.TrainerReachedPercent is not null)
                .GroupBy(item => LocalDay.DateOf(item.Session.StartedAt, window.OffsetMinutes))
                .Where(day => day.Key >= first && day.Key <= window.Today)
                .Select(day => new PracticeTempoPoint(day.Key, group.Total.SectionId, group.Total.Label, day.Max(item => item.Row.TrainerReachedPercent!.Value))))
            .OrderBy(point => point.Date).ThenBy(point => point.Label, StringComparer.Ordinal).ToList();
    }

    private sealed record SectionGroup(PracticeSectionTotal Total, IReadOnlyList<(PracticeSession Session, PracticeSectionTime Row)> Rows)
    {
        public static SectionGroup From(IReadOnlyList<(PracticeSession Session, PracticeSectionTime Row)> rows, IReadOnlyDictionary<Guid, string> currentLabels)
        {
            var latest = rows.MaxBy(item => item.Session.StartedAt).Row;
            var label = latest.SectionId is { } id && currentLabels.TryGetValue(id, out var current) ? current : latest.Label;
            var trainer = rows.Select(item => item.Row.TrainerReachedPercent).Where(percent => percent is not null).DefaultIfEmpty().Max();
            var total = new PracticeSectionTotal(
                latest.SectionId,
                latest.Kind,
                label,
                rows.Sum(item => item.Row.PlayedSeconds),
                rows.Sum(item => item.Row.LoopPasses),
                trainer,
                rows.Max(item => item.Session.EndedAt));
            return new SectionGroup(total, rows);
        }
    }
}
