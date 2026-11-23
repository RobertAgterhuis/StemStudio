using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Scores;
using static StemStudio.Domain.Tests.InstrumentViews.ChartData;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T041: chord sheet anchoring (data-model 2.6, FR-016).</summary>
public sealed class ChordSheetBuilderTests
{
    private static readonly ChordDisplay Sounding = ChordDisplay.Sounding;

    [Fact]
    public void ChordsAnchorAtTheWordTheyStartIn()
    {
        // Line 1000..3000 "Fake line one": words at 1000, 1600, 2200; C at 1000, G at 1700 (inside "line"), Am at 2300.
        var chart = Chart(("N", 1000), ("C:maj", 700), ("G:maj", 600), ("A:min", 1700));
        var lyrics = LyricsOf(SyncLevel.Word, Line(1000, 3000, ("Fake", 1000), ("line", 1600), ("one", 2200)));

        var sheet = ChordSheetBuilder.Build(new SheetSource(chart, lyrics, 0, null), Sounding);

        var row = sheet.Rows.ShouldHaveSingleItem();
        row.Text.ShouldBe("Fake line one");
        row.Chords.ShouldBe([new SheetChord(0, "C", false), new SheetChord(5, "G", false), new SheetChord(10, "Am", false)]);
    }

    [Fact]
    public void AChordBeforeTheFirstWord_IsALeadChordAtIndexZero()
    {
        var chart = Chart(("N", 900), ("D:maj", 1100));
        var lyrics = LyricsOf(SyncLevel.Word, Line(800, 2000, ("Hello", 1000), ("you", 1500)));

        var row = ChordSheetBuilder.Build(new SheetSource(chart, lyrics, 0, null), Sounding).Rows.ShouldHaveSingleItem();

        row.Chords.ShouldBe([new SheetChord(0, "D", true)]);
    }

    [Fact]
    public void IntroOutroAndLongGaps_BecomeInstrumentalRows()
    {
        // Intro C G (0..2000), line 2000..3000 with F, Am at 5200 (more than one bar after the line end), line 6500 with C, outro G.
        var chart = Chart(("C:maj", 1000), ("G:maj", 1000), ("F:maj", 1000), ("N", 2200), ("A:min", 800), ("N", 500), ("C:maj", 1000), ("G:maj", 1000));
        var lyrics = LyricsOf(SyncLevel.Word, Line(2000, 3000, ("one", 2000)), Line(6500, 7500, ("two", 6500)));

        var rows = ChordSheetBuilder.Build(new SheetSource(chart, lyrics, 0, Grid(8)), Sounding).Rows;

        rows.Select(row => (row.Text, string.Join(' ', row.Chords.Select(chord => chord.Symbol)))).ShouldBe(
        [
            (string.Empty, "C G"), ("one", "F"), (string.Empty, "Am"), ("two", "C"), (string.Empty, "G"),
        ]);
    }

    [Fact]
    public void InstrumentalRows_HoldAtMostEightChords()
    {
        var chart = Chart(Enumerable.Repeat(("C:maj", 100), 10).SelectMany(chord => new[] { chord, ("G:maj", 100) }).ToArray());

        var rows = ChordSheetBuilder.Build(new SheetSource(chart, null, 0, null), Sounding).Rows;

        rows.Select(row => row.Chords.Count).ShouldBe([8, 8, 4]);
        rows.ShouldAllBe(row => row.Text == string.Empty && row.Chords.All(chord => chord.CharIndex == 0 && !chord.Lead));
    }

    [Fact]
    public void WithoutLyrics_RowsHoldFourBarsOfTheGrid()
    {
        // 10 bars of 2 s, one chord per bar.
        var chart = Chart(Enumerable.Range(0, 10).Select(bar => (bar % 2 == 0 ? "C:maj" : "G:maj", 2000)).ToArray());

        var rows = ChordSheetBuilder.Build(new SheetSource(chart, null, 0, Grid(10)), Sounding).Rows;

        rows.Select(row => row.Chords.Count).ShouldBe([4, 4, 2]);
    }

    [Fact]
    public void PlainOrInstrumentalLyrics_CountAsNoLyrics()
    {
        var chart = Chart(("C:maj", 1000));

        ChordSheetBuilder.Build(new SheetSource(chart, LyricsOf(SyncLevel.Plain, Line(0, 1000, ("x", 0))), 0, null), Sounding).Rows.ShouldHaveSingleItem().Text.ShouldBeEmpty();
        var instrumental = LyricsOf(SyncLevel.Word) with { Instrumental = true };
        ChordSheetBuilder.Build(new SheetSource(chart, instrumental, 0, null), Sounding).Rows.ShouldHaveSingleItem().Text.ShouldBeEmpty();
    }

    [Fact]
    public void LineSyncedLyrics_AnchorAtTheNearestWordByTimeShare()
    {
        // Line 0..2000 "aa bb cc dd": the chord at 1000 is half way -> index 5.5 -> nearest word start 6 ("cc").
        var chart = Chart(("C:maj", 1000), ("G:maj", 1000));
        var lyrics = LyricsOf(SyncLevel.Line, Line(0, 2000, ("aa", null), ("bb", null), ("cc", null), ("dd", null)));

        var row = ChordSheetBuilder.Build(new SheetSource(chart, lyrics, 0, null), Sounding).Rows.ShouldHaveSingleItem();

        row.Chords.ShouldBe([new SheetChord(0, "C", false), new SheetChord(6, "G", false)]);
    }

    [Fact]
    public void TheLyricsOffset_MovesLinesAndWords()
    {
        // Offset +1000: the line starts at 1000 and its second word at 1500; G starts at 1600.
        var chart = Chart(("C:maj", 1000), ("D:maj", 600), ("G:maj", 1000));
        var lyrics = LyricsOf(SyncLevel.Word, Line(0, 1500, ("one", 0), ("two", 500)));

        var rows = ChordSheetBuilder.Build(new SheetSource(chart, lyrics, 1000, null), Sounding).Rows;

        rows[0].Text.ShouldBeEmpty("C starts before the moved line");
        rows[0].Chords.ShouldBe([new SheetChord(0, "C", false)]);
        rows[1].Chords.ShouldBe([new SheetChord(0, "D", false), new SheetChord(4, "G", false)]);
    }

    [Fact]
    public void Symbols_FollowTransposeCapoAndTheRegionKey()
    {
        var chart = Chart(("C:maj", 1000), ("A:min7/b7", 1000)) with
        {
            KeyRegions = [new KeyRegion(0, 1000, 0, KeyMode.Major, 0.9), new KeyRegion(1000, 2000, 5, KeyMode.Major, 0.9)],
        };

        var rows = ChordSheetBuilder.Build(new SheetSource(chart, null, 0, null), new ChordDisplay(1, 0)).Rows;

        rows.ShouldHaveSingleItem().Chords.Select(chord => chord.Symbol).ShouldBe(["Db", "Bbm7/Ab"]);
    }

    [Fact]
    public void WordsMissingFromTheText_AnchorAtTheCursor()
    {
        var chart = Chart(("C:maj", 500), ("G:maj", 500));
        var line = new LyricsLine(0, 1000, "abc", [Word("zzz", 0), Word("yyy", 500)]);

        var row = ChordSheetBuilder.Build(new SheetSource(chart, LyricsOf(SyncLevel.Word, line), 0, null), Sounding).Rows.ShouldHaveSingleItem();

        row.Chords.Select(chord => chord.CharIndex).ShouldBe([0, 0]);
    }

    [Fact]
    public void Build_RejectsNull() => Should.Throw<ArgumentNullException>(() => ChordSheetBuilder.Build(null!, Sounding));
}
