using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Handouts;

/// <summary>Bars of one form-chart section.</summary>
/// <param name="StartBar">1-based start bar (0 = pickup), or null without a reliable grid.</param>
/// <param name="Bars">Bars inside the section, or null.</param>
/// <param name="ChordBars">Chord symbols per bar, or null without chords or grid.</param>
public sealed record SectionBars(int? StartBar, int? Bars, IReadOnlyList<IReadOnlyList<string>>? ChordBars);

/// <summary>
/// Maps sections onto the beat grid (data-model 6.3 step 3, research R-08): the start bar from 014 when set, else from the grid
/// (index of the downbeat at or before the start, pickup = 0); bars = downbeats inside; per bar the chords sounding in it. A
/// bar that repeats the previous one keeps its symbols (the contract's chord symbols have no repeat sign; the renderer may
/// print one).
/// </summary>
public static class BarMapper
{
    /// <summary>Most bars per section.</summary>
    public const int MaxBars = 999;

    /// <summary>Most chords per bar.</summary>
    public const int MaxChordsPerBar = 8;

    /// <summary>The bars of a section.</summary>
    /// <param name="section">The section.</param>
    /// <param name="grid">The track's beat grid, or null.</param>
    /// <param name="chords">The chart with the transposition, or null.</param>
    /// <returns>The bars.</returns>
    public static SectionBars Map(SongSection section, BeatGrid? grid, (ChordChart Chart, Transposition Transposition)? chords)
    {
        ArgumentNullException.ThrowIfNull(section);
        if (grid is not { Reliable: true, Downbeats.Count: > 0 })
        {
            return new SectionBars(null, null, null);
        }

        var downbeats = grid.Downbeats.Where(downbeat => downbeat >= section.StartSeconds && downbeat < section.EndSeconds).Take(MaxBars).ToList();
        var startBar = section.StartBar ?? SectionSnapper.BarAt(section.StartSeconds, grid);
        var chordBars = chords is { } chart ? ChordBars(downbeats, grid, section.EndSeconds, chart) : null;
        return new SectionBars(startBar, downbeats.Count, chordBars);
    }

    private static List<IReadOnlyList<string>> ChordBars(List<double> downbeats, BeatGrid grid, double sectionEnd, (ChordChart Chart, Transposition Transposition) chords)
    {
        var bars = new List<IReadOnlyList<string>>(downbeats.Count);
        foreach (var start in downbeats)
        {
            var next = grid.Downbeats.FirstOrDefault(downbeat => downbeat > start, sectionEnd);
            bars.Add(Sounding(chords, start, Math.Min(next, sectionEnd)));
        }

        return bars;
    }

    private static List<string> Sounding((ChordChart Chart, Transposition Transposition) chords, double startSeconds, double endSeconds)
    {
        var (startMs, endMs) = ((int)Math.Round(startSeconds * 1000), (int)Math.Round(endSeconds * 1000));
        return chords.Chart.Segments.Where(segment => segment.HasChord && segment.StartMs < endMs && segment.EndMs > startMs)
            .Select(segment => HandoutChords.Symbol(chords.Chart, segment, chords.Transposition))
            .Aggregate(new List<string>(), (symbols, symbol) => symbols.Count > 0 && symbols[^1] == symbol ? symbols : [.. symbols, symbol])
            .Take(MaxChordsPerBar).ToList();
    }
}
