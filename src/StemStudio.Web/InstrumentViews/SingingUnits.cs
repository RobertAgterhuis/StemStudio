using System.Globalization;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;

namespace StemStudio.Web.InstrumentViews;

/// <summary>The units of a track and what one is called ("Line", "Section", "Block", "Part").</summary>
/// <param name="Kind">The unit name.</param>
/// <param name="Units">The units in time order.</param>
public sealed record UnitSet(string Kind, IReadOnlyList<SingingUnit> Units);

/// <summary>A scored unit with its name, for the banner (<c>karaoke-score</c>).</summary>
/// <param name="Kind">The unit name.</param>
/// <param name="Score">The score.</param>
public sealed record ScoredUnit(string Kind, SingingUnitScore Score)
{
    /// <summary>Short text: "Line 3: 87" or "Line 3: no score".</summary>
    public string Text => string.Create(CultureInfo.InvariantCulture, $"{Kind} {Score.Index + 1}: {Score.Score?.ToString(CultureInfo.InvariantCulture) ?? "no score"}");
}

/// <summary>What the scorer needs from the track page (the mixer cascades it to "Score this take").</summary>
/// <param name="Track">The track.</param>
/// <param name="Sections">The 014 sections, or null.</param>
/// <param name="Duration">The track length in seconds.</param>
/// <param name="TransposeSemitones">The mixer's 012 transpose: takes carry none (015), so a take is scored at the current one.</param>
public sealed record SingingSource(TrackDto Track, IReadOnlyList<SectionDto>? Sections, double Duration, int TransposeSemitones = 0);

/// <summary>
/// Scoring units of singing feedback (016 data-model 3.4): synced lyric lines, else 014 sections, else blocks of four bars
/// of the beat grid, else 10 s blocks; plus the reference contour within the engine's limits.
/// </summary>
public static class SingingUnits
{
    private const int MaxCents = 12700;
    private const int BarsPerBlock = 4;
    private const int BlockMs = 10_000;

    /// <summary>The units of a track.</summary>
    /// <param name="lyrics">The 006 lyrics, or null.</param>
    /// <param name="sections">The 014 sections, or null.</param>
    /// <param name="grid">The beat grid, or null.</param>
    /// <param name="durationSeconds">The track length.</param>
    /// <returns>The units.</returns>
    public static UnitSet From(LyricsDto? lyrics, IReadOnlyList<SectionDto>? sections, BeatGridDto? grid, double durationSeconds)
    {
        var lines = Lines(lyrics);
        return lines.Count > 0 ? new UnitSet("Line", lines)
            : sections is { Count: > 0 } ? new UnitSet("Section", Indexed(sections.Select(s => (Ms(s.StartSeconds), Ms(s.EndSeconds)))))
            : grid is { Downbeats.Count: > 1 } ? new UnitSet("Block", Bars(grid.Downbeats, Ms(durationSeconds)))
            : new UnitSet("Part", Blocks(Ms(durationSeconds)));
    }

    /// <summary>The reference contour: values outside 0..12700 count as unvoiced.</summary>
    /// <param name="document">The reference melody.</param>
    /// <returns>The contour.</returns>
    public static PitchContour Reference(VocalPitchDocumentDto document) =>
        new([.. document.Frames.Select(f => f is > 0 and <= MaxCents ? f : 0)]);

    /// <summary>The announcement of a unit score: "Line 3: 87 points".</summary>
    /// <param name="kind">The unit name.</param>
    /// <param name="score">The score.</param>
    /// <returns>The text.</returns>
    public static string Announce(string kind, SingingUnitScore score) => score.Score is { } points
        ? string.Create(CultureInfo.InvariantCulture, $"{kind} {score.Index + 1}: {points} points")
        : string.Create(CultureInfo.InvariantCulture, $"{kind} {score.Index + 1}: too little melody to score");

    private static List<SingingUnit> Lines(LyricsDto? lyrics)
    {
        if (lyrics is not { Status: LyricsStatusDto.Found, SyncLevel: SyncLevelDto.Line or SyncLevelDto.Word })
        {
            return [];
        }

        var timed = lyrics.Lines.Where(l => l.StartMs is not null && l.EndMs > l.StartMs)
            .Select(l => (l.StartMs!.Value + lyrics.OffsetMs, l.EndMs!.Value + lyrics.OffsetMs)).OrderBy(l => l.Item1);
        return Indexed(timed);
    }

    private static List<SingingUnit> Bars(IReadOnlyList<double> downbeats, int endMs)
    {
        var starts = downbeats.Where((_, i) => i % BarsPerBlock == 0).Select(Ms).ToList();
        return Indexed(starts.Select((start, i) => (start, i + 1 < starts.Count ? starts[i + 1] : endMs)));
    }

    private static List<SingingUnit> Blocks(int endMs) =>
        Indexed(Enumerable.Range(0, (endMs + BlockMs - 1) / BlockMs).Select(i => (i * BlockMs, Math.Min(endMs, (i + 1) * BlockMs))));

    private static List<SingingUnit> Indexed(IEnumerable<(int Start, int End)> ranges) =>
        [.. ranges.Where(r => r.End > r.Start).Select((r, i) => new SingingUnit(i, r.Start, r.End))];

    private static int Ms(double seconds) => (int)Math.Round(seconds * 1000);
}
