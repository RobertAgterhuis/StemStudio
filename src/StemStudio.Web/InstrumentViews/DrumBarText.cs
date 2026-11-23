using System.Globalization;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.InstrumentViews;

/// <summary>The hits of one bar as text.</summary>
/// <param name="Heading">"Bar 3", "Before bar 1" or "From 0:04 to 0:06".</param>
/// <param name="Hits">One line per hit, e.g. "Snare on 2 (loud)".</param>
public sealed record DrumBar(string Heading, IReadOnlyList<string> Hits);

/// <summary>
/// The text alternative of the hit lanes (016 FR-062, ui-test-ids <c>drum-bar-text</c>): the hits of the bar at the play
/// position with their beat ("on 2", "on 2 and"); without a beat grid, two-second windows with times.
/// </summary>
public static class DrumBarText
{
    private const double WindowSeconds = 2;

    /// <summary>The bar at a position.</summary>
    /// <param name="hits">The hits in time order.</param>
    /// <param name="grid">The beat grid, or null.</param>
    /// <param name="seconds">The play position.</param>
    /// <returns>The bar.</returns>
    public static DrumBar At(IReadOnlyList<DrumHitDto> hits, BeatGridDto? grid, double seconds)
    {
        if (grid is not { Downbeats.Count: > 0 })
        {
            var start = Math.Floor(Math.Max(0, seconds) / WindowSeconds) * WindowSeconds;
            return new DrumBar($"From {Clock(start)} to {Clock(start + WindowSeconds)}", Timed(hits, start, start + WindowSeconds));
        }

        var downbeats = grid.Downbeats;
        if (seconds < downbeats[0])
        {
            return new DrumBar("Before bar 1", Timed(hits, 0, downbeats[0]));
        }

        return InBar(hits, grid, LastAtOrBefore(downbeats, seconds));
    }

    private static DrumBar InBar(IReadOnlyList<DrumHitDto> hits, BeatGridDto grid, int bar)
    {
        var downbeats = grid.Downbeats;
        var end = bar + 1 < downbeats.Count ? downbeats[bar + 1] : downbeats[bar] + BarLength(grid);
        var lines = Between(hits, downbeats[bar], end).Select(h => $"{Piece(h.Piece)} on {Beat(grid, downbeats[bar], h.TimeMs / 1000.0)}{Strength(h)}");
        return new DrumBar(string.Create(CultureInfo.InvariantCulture, $"Bar {bar + 1}"), [.. lines]);
    }

    /// <summary>The readable name of a piece.</summary>
    /// <param name="piece">The piece.</param>
    /// <returns>E.g. "Hi-hat".</returns>
    public static string Piece(DrumPieceDto piece) => piece == DrumPieceDto.Hihat ? "Hi-hat" : piece.ToString();

    private static string[] Timed(IReadOnlyList<DrumHitDto> hits, double start, double end) =>
        [.. Between(hits, start, end).Select(h => string.Create(CultureInfo.InvariantCulture, $"{Piece(h.Piece)} at {h.TimeMs / 1000.0:0.0} s{Strength(h)}"))];

    private static IEnumerable<DrumHitDto> Between(IReadOnlyList<DrumHitDto> hits, double start, double end)
    {
        var from = FirstAtOrAfter(hits, (int)Math.Ceiling(start * 1000));
        for (var i = from; i < hits.Count && hits[i].TimeMs < end * 1000; i++)
        {
            yield return hits[i];
        }
    }

    // "1", "1 and" or the next beat when the hit is in the last quarter of a beat; beats are counted from the downbeat.
    private static string Beat(BeatGridDto grid, double downbeat, double time)
    {
        var beats = grid.Beats;
        var first = LastAtOrBefore(beats, downbeat + 1e-6);
        var index = Math.Max(first, LastAtOrBefore(beats, time));
        var length = index + 1 < beats.Count ? beats[index + 1] - beats[index] : beats[index] - beats[Math.Max(0, index - 1)];
        var fraction = length > 0 ? (time - beats[index]) / length : 0;
        var number = index - first + 1 + (fraction >= 0.75 ? 1 : 0);
        var half = fraction is >= 0.25 and < 0.75 ? " and" : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"{number}{half}");
    }

    private static double BarLength(BeatGridDto grid) => grid.Downbeats.Count > 1
        ? grid.Downbeats[^1] - grid.Downbeats[^2]
        : grid.BeatsPerBar * 60 / Math.Max(1, grid.Bpm);

    private static string Strength(DrumHitDto hit) => hit.Strength switch
    {
        >= 0.8 => " (loud)",
        < 0.4 => " (soft)",
        _ => string.Empty,
    };

    private static string Clock(double seconds)
    {
        var whole = (int)seconds;
        return string.Create(CultureInfo.InvariantCulture, $"{whole / 60}:{whole % 60:00}");
    }

    // Index of the last value <= target (0 when every value is larger).
    private static int LastAtOrBefore(IReadOnlyList<double> values, double target)
    {
        var (low, high) = (0, values.Count - 1);
        while (low < high)
        {
            var mid = (low + high + 1) / 2;
            (low, high) = values[mid] <= target ? (mid, high) : (low, mid - 1);
        }

        return low;
    }

    private static int FirstAtOrAfter(IReadOnlyList<DrumHitDto> hits, int ms)
    {
        var (low, high) = (0, hits.Count);
        while (low < high)
        {
            var mid = (low + high) / 2;
            (low, high) = hits[mid].TimeMs < ms ? (mid + 1, high) : (low, mid);
        }

        return low;
    }
}
