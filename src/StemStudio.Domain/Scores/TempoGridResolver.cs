using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Scores;

/// <summary>The time base sent with a transcription (worker contract 1.6.0 <c>TempoGrid</c>).</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="BeatsPerBar">Beats per bar.</param>
public abstract record TempoGrid(double Bpm, int BeatsPerBar);

/// <summary>The track's beat grid, shifted so that time 0 is the start of the input (beats before 0 are kept).</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="BeatsPerBar">Beats per bar.</param>
/// <param name="Beats">Beat times in seconds, ascending.</param>
/// <param name="Downbeats">Downbeat times in seconds.</param>
public sealed record BeatGridTempo(double Bpm, int BeatsPerBar, IReadOnlyList<double> Beats, IReadOnlyList<double> Downbeats) : TempoGrid(Bpm, BeatsPerBar);

/// <summary>A constant tempo entered by the user; the first downbeat is relative to the input start.</summary>
/// <param name="Bpm">Tempo.</param>
/// <param name="BeatsPerBar">Beats per bar.</param>
/// <param name="FirstDownbeatSeconds">First downbeat.</param>
public sealed record ManualTempoGrid(double Bpm, int BeatsPerBar, double FirstDownbeatSeconds) : TempoGrid(Bpm, BeatsPerBar);

/// <summary>Chooses the grid of a score (data-model 4.3): a manual tempo wins, else a reliable beat grid, else the tempo is required.</summary>
public static class TempoGridResolver
{
    /// <summary>Whether a beat grid can be used (at least two beats and one downbeat).</summary>
    /// <param name="grid">The track's grid, or null.</param>
    /// <returns>The status.</returns>
    public static GridStatus StatusOf(BeatGrid? grid) => grid switch
    {
        null or { Beats.Count: < 2 } or { Downbeats.Count: 0 } => GridStatus.None,
        { Reliable: true } => GridStatus.Reliable,
        _ => GridStatus.Unreliable,
    };

    /// <summary>The shift of the track grid: the start of the audio item's export range plus the score's own range start.</summary>
    /// <param name="assetRangeStart">Range start of the source audio item, or null.</param>
    /// <param name="scoreRangeStart">Range start of the score, or null.</param>
    /// <returns>Seconds.</returns>
    public static double Offset(double? assetRangeStart, double? scoreRangeStart) => (assetRangeStart ?? 0) + (scoreRangeStart ?? 0);

    /// <summary>The grid to send.</summary>
    /// <param name="grid">The track's beat grid, or null (no track or no analysis).</param>
    /// <param name="tempo">A manual tempo, or null.</param>
    /// <param name="offsetSeconds">Seconds of the track before the input starts.</param>
    /// <returns>The grid, or null when the tempo is required (422 <c>tempo-required</c>).</returns>
    public static TempoGrid? Resolve(BeatGrid? grid, ManualTempo? tempo, double offsetSeconds)
    {
        if (tempo is not null)
        {
            return new ManualTempoGrid(tempo.Bpm, tempo.BeatsPerBar, tempo.FirstDownbeatSeconds);
        }

        return StatusOf(grid) == GridStatus.Reliable
            ? new BeatGridTempo(grid!.Bpm, grid.BeatsPerBar, Shift(grid.Beats, offsetSeconds), Shift(grid.Downbeats, offsetSeconds))
            : null;
    }

    private static List<double> Shift(IReadOnlyList<double> times, double offset) => times.Select(time => Math.Round(time - offset, 6)).ToList();
}
