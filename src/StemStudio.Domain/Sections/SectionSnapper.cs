using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Sections;

/// <summary>Snaps section boundaries to the grid (014 FR-003) and finds bar numbers.</summary>
public static class SectionSnapper
{
    private const double BarTolerance = 1e-3;

    /// <summary>The snap mode for a grid: downbeats when reliable, beats when unreliable, 0.1 s without a grid.</summary>
    /// <param name="grid">The track's beat grid, or null.</param>
    /// <returns>The mode.</returns>
    public static SectionSnap ModeFor(BeatGrid? grid) => grid switch
    {
        { Reliable: true, Downbeats.Count: > 0 } => SectionSnap.Downbeat,
        { Beats.Count: > 0 } => SectionSnap.Beat,
        _ => SectionSnap.Time,
    };

    /// <summary>The nearest snap point of a time.</summary>
    /// <param name="seconds">Time in track seconds.</param>
    /// <param name="grid">The track's beat grid, or null.</param>
    /// <returns>The snapped time.</returns>
    public static double Snap(double seconds, BeatGrid? grid) => ModeFor(grid) switch
    {
        SectionSnap.Downbeat => Nearest(grid!.Downbeats, seconds),
        SectionSnap.Beat => Nearest(grid!.Beats, seconds),
        _ => Math.Round((Math.Max(0, seconds) * 10) + 1e-9, MidpointRounding.AwayFromZero) / 10,
    };

    /// <summary>The bar number at a time: the count of downbeats up to it (0 = before the first downbeat, a pickup).</summary>
    /// <param name="seconds">Time in track seconds.</param>
    /// <param name="grid">The track's beat grid, or null.</param>
    /// <returns>The bar number, or null without downbeats.</returns>
    public static int? BarAt(double seconds, BeatGrid? grid) =>
        grid is { Downbeats.Count: > 0 } ? grid.Downbeats.Count(downbeat => downbeat <= seconds + BarTolerance) : null;

    private static double Nearest(IReadOnlyList<double> points, double seconds) =>
        points.MinBy(point => Math.Abs(point - seconds));
}
