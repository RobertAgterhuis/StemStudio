using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Handouts;

/// <summary>A chord over a lyric line.</summary>
/// <param name="Symbol">Chord symbol (ASCII accidentals).</param>
/// <param name="CharIndex">Character index.</param>
public sealed record LyricChord(string Symbol, int CharIndex);

/// <summary>A lyric line with chords; empty text = an instrumental chord row (016 chord sheet rows).</summary>
/// <param name="Text">Text (at most 500 characters).</param>
/// <param name="Section">Index into the sections, or null.</param>
/// <param name="Chords">Chords (at most 32).</param>
public sealed record LyricLine(string Text, int? Section, IReadOnlyList<LyricChord> Chords);

/// <summary>How chords were placed (the 016 chord sheet rule).</summary>
public enum ChordPlacement
{
    /// <summary>At the word they start in (word-synced lyrics).</summary>
    Word,

    /// <summary>At the word nearest their share of the line (line-synced lyrics).</summary>
    Line,

    /// <summary>No chords placed.</summary>
    None,
}

/// <summary>The lyric lines of a handout and how chords were placed.</summary>
/// <param name="Lines">Lines (at most 1000).</param>
/// <param name="Placement">Placement.</param>
public sealed record HandoutLyricLines(IReadOnlyList<LyricLine> Lines, ChordPlacement Placement);

/// <summary>
/// Chords of a handout as an adapter over 016's domain (no own parser, data-model 6.3 step 2 and 4): symbols spelled by
/// <see cref="ChordSpeller"/> with <see cref="ChordDisplay"/> for the melodic transposition, keys by <see cref="KeyFormatter"/>,
/// chords over lyrics by <see cref="ChordSheetBuilder"/>.
/// </summary>
public static class HandoutChords
{
    /// <summary>Longest lyric line text.</summary>
    public const int MaxLineLength = 500;

    /// <summary>Most chords on one lyric line.</summary>
    public const int MaxChordsPerLine = 32;

    /// <summary>Most lyric lines.</summary>
    public const int MaxLines = 1000;

    /// <summary>Most distinct chord symbols in the legend.</summary>
    public const int MaxSymbols = 64;

    /// <summary>The semitones a part is written above concert pitch: B-flat +2, E-flat +9, F +7.</summary>
    /// <param name="transposition">Transposition.</param>
    /// <returns>Semitones.</returns>
    public static int Semitones(Transposition transposition) => transposition switch
    {
        Transposition.BFlat => 2,
        Transposition.EFlat => 9,
        Transposition.F => 7,
        _ => 0,
    };

    /// <summary>The 016 display of a transposition (no capo).</summary>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The display.</returns>
    public static ChordDisplay Display(Transposition transposition) => new(Semitones(transposition), 0);

    /// <summary>The note of a transposed part, e.g. "Part in B-flat (sounds a major second lower)"; null for concert pitch.</summary>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The note or null.</returns>
    public static string? TranspositionNote(Transposition transposition) => transposition switch
    {
        Transposition.BFlat => "Part in B-flat (sounds a major second lower)",
        Transposition.EFlat => "Part in E-flat (sounds a major sixth lower)",
        Transposition.F => "Part in F (sounds a perfect fifth lower)",
        _ => null,
    };

    /// <summary>The symbol of one segment in the written key of the part.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="segment">A segment of it.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The symbol, or <c>N.C.</c> without a chord.</returns>
    public static string Symbol(ChordChart chart, ChordSegment segment, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(segment);
        return ChordSpeller.Symbol(segment.Chord, chart.SpellingKeyAt(segment.StartMs), Display(transposition));
    }

    /// <summary>The distinct chord symbols in order of appearance (the legend line, at most 64).</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The symbols.</returns>
    public static IReadOnlyList<string> Legend(ChordChart chart, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(chart);
        return chart.Segments.Where(segment => segment.HasChord).Select(segment => Symbol(chart, segment, transposition)).Distinct(StringComparer.Ordinal).Take(MaxSymbols).ToList();
    }

    /// <summary>The song's key in the written key of the part, e.g. "B minor"; null without a key.</summary>
    /// <param name="chart">The chart, or null.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The key text or null.</returns>
    public static string? Key(ChordChart? chart, Transposition transposition) =>
        chart?.Key is { } key ? KeyFormatter.Name(ChordSpeller.Move(key.Key, Display(transposition))!.Value) : null;

    /// <summary>Chords over lyrics from 016's chord sheet (word or line placement by the lyrics' sync level).</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="lyrics">The lyrics with their offset.</param>
    /// <param name="grid">The beat grid (instrumental rows), or null.</param>
    /// <param name="transposition">Transposition.</param>
    /// <returns>The lines.</returns>
    public static HandoutLyricLines OverLyrics(ChordChart chart, (LyricsDocument Document, int OffsetMs) lyrics, BeatGrid? grid, Transposition transposition)
    {
        ArgumentNullException.ThrowIfNull(chart);
        ArgumentNullException.ThrowIfNull(lyrics.Document);
        var sheet = ChordSheetBuilder.Build(new SheetSource(chart, lyrics.Document, lyrics.OffsetMs, grid), Display(transposition));
        var lines = sheet.Rows.Take(MaxLines)
            .Select(row => new LyricLine(Cut(row.Text), null, row.Chords.Take(MaxChordsPerLine).Select(chord => new LyricChord(chord.Symbol, Math.Min(chord.CharIndex, MaxLineLength))).ToList()))
            .ToList();
        var placement = lyrics.Document.SyncLevel switch
        {
            SyncLevel.Word => ChordPlacement.Word,
            SyncLevel.Line => ChordPlacement.Line,
            _ => ChordPlacement.None,
        };
        return new HandoutLyricLines(lines, lines.Any(line => line.Chords.Count > 0) ? placement : ChordPlacement.None);
    }

    /// <summary>Plain lyric lines without chords.</summary>
    /// <param name="document">The lyrics.</param>
    /// <returns>The lines.</returns>
    public static HandoutLyricLines Plain(LyricsDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new HandoutLyricLines(document.Lines.Take(MaxLines).Select(line => new LyricLine(Cut(line.Text), null, [])).ToList(), ChordPlacement.None);
    }

    private static string Cut(string text) => text.Length <= MaxLineLength ? text : text[..MaxLineLength];
}
