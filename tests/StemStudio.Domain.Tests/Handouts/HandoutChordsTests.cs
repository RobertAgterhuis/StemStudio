using System.Text.RegularExpressions;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Scores;
using static StemStudio.Domain.Tests.InstrumentViews.ChartData;

namespace StemStudio.Domain.Tests.Handouts;

/// <summary>
/// Feature 020 T056 and T058: handout chords over 016's domain (spelling, transposition, keys, notes) and chords over lyrics
/// and bars (016 chord sheet rows, <see cref="BarMapper"/>).
/// </summary>
public sealed partial class HandoutChordsTests
{
    [Theory]
    [InlineData(Transposition.C, "C", "Am", "F/C")]
    [InlineData(Transposition.BFlat, "D", "Bm", "G/D")]
    [InlineData(Transposition.EFlat, "A", "F#m", "D/A")]
    [InlineData(Transposition.F, "G", "Em", "C/G")]
    public void Symbols_AreWrittenInThePartsKey(Transposition transposition, string first, string second, string third)
    {
        var chart = Chart(("C:maj", 1000), ("A:min", 1000), ("F:maj/5", 1000), ("N", 500));

        HandoutChords.Legend(chart, transposition).ShouldBe([first, second, third]);
        HandoutChords.Symbol(chart, chart.Segments[3], transposition).ShouldBe("N.C.");
    }

    [Fact]
    public void FlatKeys_UseFlats_AndEverySymbolMatchesTheContractPattern()
    {
        var chart = Chart(("F:maj", 500), ("Bb:maj", 500), ("D:min7", 500), ("G:hdim7", 500), ("C:minmaj7", 500), ("Eb:dim7", 500)) with { Key = new SongKey(5, KeyMode.Major, 0.9, null), KeyRegions = [] };

        var symbols = HandoutChords.Legend(chart, Transposition.C);

        symbols.ShouldBe(["F", "Bb", "Dm7", "Gm7b5", "Cm(maj7)", "Ebdim7"]);
        symbols.ShouldAllBe(symbol => SymbolPattern().IsMatch(symbol) && symbol.Length <= 20);
    }

    [Fact]
    public void TheKey_IsMovedWithThePart_AndNullWithoutOne()
    {
        var chart = Chart(("A:min", 1000)) with { Key = new SongKey(9, KeyMode.Minor, 0.8, null) };

        HandoutChords.Key(chart, Transposition.C).ShouldBe("A minor");
        HandoutChords.Key(chart, Transposition.BFlat).ShouldBe("B minor");
        HandoutChords.Key(chart with { Key = null }, Transposition.C).ShouldBeNull();
        HandoutChords.Key(null, Transposition.F).ShouldBeNull();
    }

    [Fact]
    public void TranspositionNotes_AndSemitones()
    {
        HandoutChords.TranspositionNote(Transposition.BFlat).ShouldBe("Part in B-flat (sounds a major second lower)");
        HandoutChords.TranspositionNote(Transposition.EFlat).ShouldBe("Part in E-flat (sounds a major sixth lower)");
        HandoutChords.TranspositionNote(Transposition.F).ShouldBe("Part in F (sounds a perfect fifth lower)");
        HandoutChords.TranspositionNote(Transposition.C).ShouldBeNull();
        (HandoutChords.Semitones(Transposition.BFlat), HandoutChords.Semitones(Transposition.EFlat), HandoutChords.Semitones(Transposition.F), HandoutChords.Semitones(Transposition.C)).ShouldBe((2, 9, 7, 0));
    }

    [Fact]
    public void WordSyncedLyrics_PlaceChordsAtTheirWord_AndInstrumentalRowsHaveEmptyText()
    {
        var chart = Chart(("C:maj", 2000), ("G:maj", 2000), ("A:min", 4000), ("F:maj", 4000));
        var lyrics = LyricsOf(SyncLevel.Word, Line(0, 3900, ("Hello", 0), ("world", 2000)));

        var result = HandoutChords.OverLyrics(chart, (lyrics, 0), Grid(8), Transposition.C);

        result.Placement.ShouldBe(ChordPlacement.Word);
        result.Lines[0].ShouldBe(new LyricLine("Hello world", null, [new LyricChord("C", 0), new LyricChord("G", 6)]), new LineComparer());
        result.Lines.Skip(1).ShouldAllBe(line => line.Text.Length == 0 && line.Chords.Count > 0);
    }

    [Fact]
    public void LineSyncedLyrics_AreLinePlaced_AndTransposed()
    {
        var chart = Chart(("C:maj", 2000), ("G:maj", 2000));
        var lyrics = LyricsOf(SyncLevel.Line, Line(0, 4000, ("Hello", null), ("world", null)));

        var result = HandoutChords.OverLyrics(chart, (lyrics, 0), null, Transposition.BFlat);

        result.Placement.ShouldBe(ChordPlacement.Line);
        result.Lines[0].Chords.Select(chord => chord.Symbol).ShouldBe(["D", "A"]);
    }

    [Fact]
    public void AChartWithoutChords_PlacesNone_AndPlainLyricsHaveNoChords()
    {
        var lyrics = LyricsOf(SyncLevel.Word, Line(0, 1000, ("Hi", 0)));

        HandoutChords.OverLyrics(Chart(("N", 2000)), (lyrics, 0), null, Transposition.C).Placement.ShouldBe(ChordPlacement.None);
        var plain = HandoutChords.Plain(lyrics with { Lines = [.. lyrics.Lines, new LyricsLine(null, null, new string('x', 600), [])] });
        (plain.Placement, plain.Lines.Count, plain.Lines[1].Text.Length).ShouldBe((ChordPlacement.None, 2, HandoutChords.MaxLineLength));
        plain.Lines.ShouldAllBe(line => line.Chords.Count == 0);
    }

    [Fact]
    public void Bars_ComeFromTheGrid_WithChordsPerBar()
    {
        var chart = Chart(("C:maj", 2000), ("C:maj", 2000), ("G:maj", 1000), ("F:maj", 1000));
        var section = new SongSection(Guid.NewGuid(), "Verse", 0, 6, null);

        var bars = BarMapper.Map(section, Grid(8), (chart, Transposition.C));

        (bars.StartBar, bars.Bars).ShouldBe(((int?)1, (int?)3));
        bars.ChordBars!.Select(bar => string.Join(' ', bar)).ShouldBe(["C", "C", "G F"]);
    }

    [Fact]
    public void ThePickup_IsBarZero_And014sStartBarWins()
    {
        var grid = Grid(8) with { Downbeats = [1, 3, 5, 7] };

        BarMapper.Map(new SongSection(Guid.NewGuid(), "Intro", 0, 3, null), grid, null).ShouldBe(new SectionBars(0, 1, null));
        BarMapper.Map(new SongSection(Guid.NewGuid(), "Verse", 3, 7, 9), grid, null).StartBar.ShouldBe(9);
    }

    [Fact]
    public void WithoutAReliableGrid_ThereAreNoBars()
    {
        var section = new SongSection(Guid.NewGuid(), "Verse", 0, 8, 3);

        BarMapper.Map(section, Grid(4) with { Reliable = false }, null).ShouldBe(new SectionBars(null, null, null));
        BarMapper.Map(section, null, null).ShouldBe(new SectionBars(null, null, null));
    }

    [Fact]
    public void NullArguments_AreRefused()
    {
        var chart = Chart(("C:maj", 1000));
        Should.Throw<ArgumentNullException>(() => HandoutChords.Legend(null!, Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutChords.Symbol(null!, chart.Segments[0], Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutChords.Symbol(chart, null!, Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutChords.OverLyrics(null!, (LyricsOf(SyncLevel.Word), 0), null, Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutChords.OverLyrics(chart, (null!, 0), null, Transposition.C));
        Should.Throw<ArgumentNullException>(() => HandoutChords.Plain(null!));
        Should.Throw<ArgumentNullException>(() => BarMapper.Map(null!, null, null));
    }

    [GeneratedRegex(@"^[A-G](#|b)?[A-Za-z0-9+#b()-]{0,12}(/[A-G](#|b)?)?$|^N\.C\.$")]
    private static partial Regex SymbolPattern();

    private sealed class LineComparer : IEqualityComparer<LyricLine>
    {
        public bool Equals(LyricLine? x, LyricLine? y) => x!.Text == y!.Text && x.Section == y.Section && x.Chords.SequenceEqual(y.Chords);

        public int GetHashCode(LyricLine obj) => obj.Text.GetHashCode(StringComparison.Ordinal);
    }
}
