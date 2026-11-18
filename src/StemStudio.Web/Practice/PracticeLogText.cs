using System.Globalization;
using StemStudio.Contracts.Practice;

namespace StemStudio.Web.Practice;

/// <summary>Texts of the practice log (014 US6, FR-027): durations and the chart summaries that stand in for the charts.</summary>
public static class PracticeLogText
{
    /// <summary>A practice duration: "0 min", "12 min", "2 h 5 min".</summary>
    /// <param name="seconds">Seconds.</param>
    /// <returns>The text.</returns>
    public static string Duration(double seconds)
    {
        var minutes = (int)Math.Floor(Math.Max(0, seconds) / 60);
        return minutes < 60
            ? string.Create(CultureInfo.InvariantCulture, $"{minutes} min")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes / 60} h {minutes % 60} min");
    }

    /// <summary>The summary of the minutes-per-day chart.</summary>
    /// <param name="days">The days of the window.</param>
    /// <returns>For example "Practised on 2 of the last 30 days, 22 min in total; most on 17 Nov (12 min)."</returns>
    public static string DaysSummary(IReadOnlyList<PracticeDayDto> days)
    {
        ArgumentNullException.ThrowIfNull(days);
        var active = days.Where(d => d.Seconds > 0).ToList();
        if (active.Count == 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"No practice in the last {days.Count} days.");
        }

        var best = active.MaxBy(d => d.Seconds)!;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"Practised on {active.Count} of the last {days.Count} days, {Duration(active.Sum(d => d.Seconds))} in total; most on {Day(best.Date)} ({Duration(best.Seconds)}).");
    }

    /// <summary>The summary of the tempo chart: per section the first and the best reached speed.</summary>
    /// <param name="tempo">The tempo points.</param>
    /// <returns>For example "Guitar solo: 85 % to 95 %." or a note when there is none.</returns>
    public static string TempoSummary(IReadOnlyList<PracticeTempoPointDto> tempo)
    {
        ArgumentNullException.ThrowIfNull(tempo);
        if (tempo.Count == 0)
        {
            return "No speed trainer passes over a whole section yet.";
        }

        return string.Join(' ', tempo.GroupBy(t => t.Label).Select(g =>
            string.Create(CultureInfo.InvariantCulture, $"{g.Key}: {g.OrderBy(t => t.Date).First().Percent:0} % to {g.Max(t => t.Percent):0} %.")));
    }

    /// <summary>A short day text ("17 Nov").</summary>
    /// <param name="date">The date.</param>
    /// <returns>The text.</returns>
    public static string Day(DateOnly date) => date.ToString("d MMM", CultureInfo.InvariantCulture);
}
