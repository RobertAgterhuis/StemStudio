using StemStudio.Contracts.InstrumentViews;

namespace StemStudio.Web.InstrumentViews;

/// <summary>One piece of a chord sheet row: the text under a chord (or before the first chord).</summary>
/// <param name="Chord">The chord placed above <see cref="Text"/>, or null for text before the first chord.</param>
/// <param name="ChordIndex">Index of the chord in the row (-1 without a chord).</param>
/// <param name="Text">The lyric text up to the next chord.</param>
public sealed record ChordSheetPart(ChordSheetChordDto? Chord, int ChordIndex, string Text);

/// <summary>Lays out a chord sheet row as chord-over-text pieces (data-model 2.6: chords anchored at character indexes).</summary>
public static class ChordSheetLayout
{
    /// <summary>Splits the row text at the chord positions.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The pieces in reading order.</returns>
    public static IReadOnlyList<ChordSheetPart> Parts(ChordSheetRowDto row)
    {
        var text = row.Text;
        var first = row.Chords.Count == 0 ? text.Length : Index(row.Chords[0], text);
        var parts = first > 0 ? new List<ChordSheetPart> { new(null, -1, text[..first]) } : [];
        parts.AddRange(row.Chords.Select((chord, m) => new ChordSheetPart(chord, m, text[Index(chord, text)..End(row, m, text)])));
        return parts;
    }

    // The end of the m-th chord's text: the next chord's position (never before its own) or the end of the row.
    private static int End(ChordSheetRowDto row, int m, string text) =>
        m + 1 < row.Chords.Count ? Math.Max(Index(row.Chords[m], text), Index(row.Chords[m + 1], text)) : text.Length;

    private static int Index(ChordSheetChordDto chord, string text) => Math.Clamp(chord.CharIndex, 0, text.Length);
}

/// <summary>
/// Maps the chords of a chord sheet to the segments of the chart, for the edit buttons (FR-015): the sheet lists the
/// chords in time order, so the n-th chord is the n-th segment with a chord (or the n-th segment when the sheet also
/// lists "N.C."). When the counts do not match, no chord can be edited from the sheet.
/// </summary>
public sealed class ChordSheetSegments
{
    private readonly Dictionary<(int Row, int Chord), int> _segments;

    private ChordSheetSegments(Dictionary<(int Row, int Chord), int> segments) => _segments = segments;

    /// <summary>Builds the map.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="document">The chart.</param>
    /// <returns>The map, or null when the sheet and the chart do not line up.</returns>
    public static ChordSheetSegments? Build(ChordSheetDto sheet, ChordChartDocumentDto document)
    {
        var chords = sheet.Rows.SelectMany((row, r) => row.Chords.Select((_, m) => (r, m))).ToList();
        var withChord = document.Segments.Select((s, i) => (s, i)).Where(x => x.s.Root is not null).Select(x => x.i).ToList();
        var all = Enumerable.Range(0, document.Segments.Count).ToList();
        var indexes = chords.Count == withChord.Count ? withChord : chords.Count == all.Count ? all : null;
        return indexes is null ? null : new ChordSheetSegments(chords.Zip(indexes).ToDictionary(x => x.First, x => x.Second));
    }

    /// <summary>The segment of a chord of the sheet.</summary>
    /// <param name="row">Row index.</param>
    /// <param name="chord">Chord index in the row.</param>
    /// <returns>The segment index, or null.</returns>
    public int? ForChord(int row, int chord) => _segments.TryGetValue((row, chord), out var index) ? index : null;
}
