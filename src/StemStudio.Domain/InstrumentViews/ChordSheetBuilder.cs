using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.InstrumentViews;

/// <summary>A chord placed in a sheet row.</summary>
/// <param name="CharIndex">Character index in the row text (0 in instrumental rows).</param>
/// <param name="Symbol">Display symbol.</param>
/// <param name="Lead">Starts before the first word of the line.</param>
public sealed record SheetChord(int CharIndex, string Symbol, bool Lead);

/// <summary>One sheet row: a lyric line with its chords, or an instrumental row (empty text).</summary>
/// <param name="Text">Lyric text, "" for instrumental rows.</param>
/// <param name="Chords">Chords in time order.</param>
public sealed record SheetRow(string Text, IReadOnlyList<SheetChord> Chords);

/// <summary>A chord sheet (data-model 2.6).</summary>
/// <param name="Display">Transpose and capo it is spelled for.</param>
/// <param name="Rows">Rows in time order.</param>
public sealed record ChordSheet(ChordDisplay Display, IReadOnlyList<SheetRow> Rows);

/// <summary>What a chord sheet is built from.</summary>
/// <param name="Chart">The chord chart.</param>
/// <param name="Lyrics">The found lyrics document, or null (no lyrics, plain text is treated as none).</param>
/// <param name="LyricsOffsetMs">The 006 user offset (positive = lyrics later).</param>
/// <param name="Grid">The beat grid, or null.</param>
public sealed record SheetSource(ChordChart Chart, LyricsDocument? Lyrics, int LyricsOffsetMs, BeatGrid? Grid);

/// <summary>
/// Builds the chord sheet of data-model 2.6: chords over the lyric lines anchored at the word they start in (lead chords at
/// index 0), instrumental rows of at most 8 chords for the intro, the outro (after the last line's end) and gaps of more than one
/// bar after a line, and rows of 4 bars
/// (or 8 chords) without lyrics. Line-synced lyrics without word times anchor at the word nearest the chord's share of the
/// line.
/// </summary>
public static class ChordSheetBuilder
{
    /// <summary>Most chords in an instrumental row.</summary>
    public const int ChordsPerInstrumentalRow = 8;

    /// <summary>Bars per row without lyrics.</summary>
    public const int BarsPerRow = 4;

    /// <summary>Most rows (contract <c>MaxChordSheetRows</c>).</summary>
    public const int MaxRows = 2000;

    /// <summary>Most chords per row (contract <c>MaxChordsPerSheetRow</c>).</summary>
    public const int MaxChordsPerRow = 40;

    /// <summary>Bar length when there is no grid.</summary>
    public const int DefaultBarMs = 2000;

    /// <summary>Builds the sheet.</summary>
    /// <param name="source">Chart, lyrics and grid.</param>
    /// <param name="display">Transpose and capo.</param>
    /// <returns>The sheet.</returns>
    public static ChordSheet Build(SheetSource source, ChordDisplay display)
    {
        ArgumentNullException.ThrowIfNull(source);
        var chords = source.Chart.Segments.Where(segment => segment.HasChord)
            .Select(segment => new PlacedChord(segment.StartMs, ChordSpeller.Symbol(segment.Chord, source.Chart.SpellingKeyAt(segment.StartMs), display)))
            .ToList();
        var lines = TimedLines(source);
        var rows = lines.Count == 0 ? WithoutLyrics(chords, source.Grid) : WithLyrics(chords, lines, BarMs(source.Grid));
        return new ChordSheet(display, rows.Take(MaxRows).ToList());
    }

    private static List<TimedLine> TimedLines(SheetSource source)
    {
        if (source.Lyrics is not { SyncLevel: not SyncLevel.Plain, Instrumental: false } lyrics)
        {
            return [];
        }

        return lyrics.Lines.Where(line => line.StartMs is not null)
            .Select(line => new TimedLine(line.StartMs!.Value + source.LyricsOffsetMs, (line.EndMs ?? line.StartMs!.Value) + source.LyricsOffsetMs, source.LyricsOffsetMs, line))
            .OrderBy(line => line.StartMs)
            .ToList();
    }

    private static int BarMs(BeatGrid? grid) =>
        grid is { Bpm: > 0 } ? (int)Math.Round(60_000 / grid.Bpm * grid.BeatsPerBar) : DefaultBarMs;

    private static List<SheetRow> WithLyrics(List<PlacedChord> chords, List<TimedLine> lines, int barMs)
    {
        var rows = new List<SheetRow>();
        var pending = new List<PlacedChord>();
        var next = 0;
        foreach (var (line, end) in lines.Select((line, index) => (line, index + 1 < lines.Count ? lines[index + 1].StartMs : int.MaxValue)))
        {
            rows.AddRange(Flush(pending, Before(chords, ref next, line.StartMs)));
            rows.Add(LineRow(line, Before(chords, ref next, end == int.MaxValue ? line.EndMs : Math.Min(end, line.EndMs + barMs))));
            pending.AddRange(Before(chords, ref next, end));
        }

        rows.AddRange(Flush(pending, chords.Skip(next)));
        return rows;
    }

    /// <summary>The instrumental rows of the pending chords and the ones that follow them; the pending list is emptied.</summary>
    private static List<SheetRow> Flush(List<PlacedChord> pending, IEnumerable<PlacedChord> more)
    {
        pending.AddRange(more);
        var rows = Instrumental(pending).ToList();
        pending.Clear();
        return rows;
    }

    private static List<PlacedChord> Before(List<PlacedChord> chords, ref int next, int endMs)
    {
        var start = next;
        while (next < chords.Count && chords[next].StartMs < endMs)
        {
            next++;
        }

        return chords.GetRange(start, next - start);
    }

    private static IEnumerable<SheetRow> Instrumental(List<PlacedChord> chords) =>
        chords.Chunk(ChordsPerInstrumentalRow).Select(chunk => new SheetRow(string.Empty, chunk.Select(chord => new SheetChord(0, chord.Symbol, false)).ToList()));

    private static SheetRow LineRow(TimedLine line, List<PlacedChord> chords)
    {
        var anchors = WordAnchors(line);
        var placed = chords.Take(MaxChordsPerRow).Select(chord => Anchor(line, anchors, chord)).ToList();
        return new SheetRow(line.Line.Text, placed);
    }

    private static SheetChord Anchor(TimedLine line, List<WordAnchor> anchors, PlacedChord chord)
    {
        var timed = anchors.Where(anchor => anchor.StartMs is not null).ToList();
        if (timed.Count == 0)
        {
            return new SheetChord(Proportional(line, anchors, chord.StartMs), chord.Symbol, false);
        }

        var containing = timed.LastOrDefault(anchor => anchor.StartMs <= chord.StartMs);
        return containing is null ? new SheetChord(0, chord.Symbol, true) : new SheetChord(containing.CharIndex, chord.Symbol, false);
    }

    private static int Proportional(TimedLine line, List<WordAnchor> anchors, int chordMs)
    {
        var span = Math.Max(line.EndMs - line.StartMs, 1);
        var share = Math.Clamp((chordMs - line.StartMs) / (double)span, 0, 1);
        var target = share * line.Line.Text.Length;
        return anchors.Count == 0 ? 0 : anchors.MinBy(anchor => Math.Abs(anchor.CharIndex - target))!.CharIndex;
    }

    private static List<WordAnchor> WordAnchors(TimedLine timed)
    {
        var line = timed.Line;
        var anchors = new List<WordAnchor>();
        var cursor = 0;
        foreach (var word in line.Words)
        {
            var found = line.Text.IndexOf(word.Text, cursor, StringComparison.Ordinal);
            var at = found < 0 ? Math.Min(cursor, Math.Max(line.Text.Length - 1, 0)) : found;
            anchors.Add(new WordAnchor(at, word.StartMs is { } start ? start + timed.OffsetMs : null));
            cursor = found < 0 ? cursor : found + word.Text.Length;
        }

        return anchors;
    }

    private static List<SheetRow> WithoutLyrics(List<PlacedChord> chords, BeatGrid? grid)
    {
        if (grid is not { Reliable: true, Downbeats.Count: > BarsPerRow })
        {
            return Instrumental(chords).ToList();
        }

        var rows = new List<SheetRow>();
        var next = 0;
        for (var bar = BarsPerRow; bar < grid.Downbeats.Count; bar += BarsPerRow)
        {
            AddRow(rows, Before(chords, ref next, (int)Math.Round(grid.Downbeats[bar] * 1000)));
        }

        AddRow(rows, chords.Skip(next).ToList());
        return rows;
    }

    private static void AddRow(List<SheetRow> rows, List<PlacedChord> chords)
    {
        if (chords.Count > 0)
        {
            rows.Add(new SheetRow(string.Empty, chords.Take(MaxChordsPerRow).Select(chord => new SheetChord(0, chord.Symbol, false)).ToList()));
        }
    }

    private sealed record PlacedChord(int StartMs, string Symbol);

    private sealed record TimedLine(int StartMs, int EndMs, int OffsetMs, LyricsLine Line);

    private sealed record WordAnchor(int CharIndex, int? StartMs);
}
