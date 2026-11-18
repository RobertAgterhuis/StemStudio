using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Domain.Tests.InstrumentViews;

/// <summary>Feature 016 T041: ChordPro directives and inline chords (data-model 2.6, FR-016).</summary>
public sealed class ChordProWriterTests
{
    private static readonly ChordSheet Sheet = new(
        new ChordDisplay(0, 2),
        [
            new SheetRow(string.Empty, [new SheetChord(0, "A", false), new SheetChord(0, "E", false)]),
            new SheetRow("Fake line one", [new SheetChord(0, "D", true), new SheetChord(0, "A", false), new SheetChord(5, "E", false)]),
        ]);

    [Fact]
    public void Write_HasDirectivesAndInlineChords()
    {
        var text = ChordProWriter.Write(new ChordProHeader("Fake Song", "Fake Artist", new MusicalKey(11, KeyMode.Major)), Sheet);

        text.ShouldBe("{title: Fake Song}\n{artist: Fake Artist}\n{capo: 2}\n{key: A}\n[A] [E]\n[D][A]Fake [E]line one\n");
    }

    [Fact]
    public void Write_WithoutArtistCapoOrKey_LeavesThoseDirectivesOut()
    {
        var sheet = new ChordSheet(ChordDisplay.Sounding, [new SheetRow("x", [new SheetChord(1, "Am", false)])]);

        ChordProWriter.Write(new ChordProHeader("Song", " ", null), sheet).ShouldBe("{title: Song}\nx[Am]\n");
    }

    [Fact]
    public void Write_MinorKey_HasTheMinorSuffix() =>
        ChordProWriter.Write(new ChordProHeader("S", null, new MusicalKey(9, KeyMode.Minor)), new ChordSheet(ChordDisplay.Sounding, []))
            .ShouldBe("{title: S}\n{key: Am}\n");

    [Fact]
    public void HostileTitleAndLyrics_CannotOpenDirectivesOrChords()
    {
        var sheet = new ChordSheet(ChordDisplay.Sounding, [new SheetRow("a [Fake] {capo: 9}\r\nline", [new SheetChord(99, "C", false)])]);

        var text = ChordProWriter.Write(new ChordProHeader("T}\n{capo: 7", "[A]rtist", null), sheet);

        text.ShouldBe("{title: T) (capo: 7}\n{artist: (A)rtist}\na (Fake) (capo: 9)  line[C]\n");
        text.Split('\n').Count(line => line.StartsWith('{')).ShouldBe(2);
    }

    [Fact]
    public void Write_RejectsNulls()
    {
        Should.Throw<ArgumentNullException>(() => ChordProWriter.Write(null!, Sheet));
        Should.Throw<ArgumentNullException>(() => ChordProWriter.Write(new ChordProHeader("t", null, null), null!));
        Should.Throw<ArgumentNullException>(() => ChordProWriter.Safe(null!));
    }
}
