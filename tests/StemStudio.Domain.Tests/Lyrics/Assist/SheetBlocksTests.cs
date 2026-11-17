using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T012: blocks of at most 8 lyric lines; sections and blank lines end a block, short neighbours are merged up to 8 lines (CTO 023 English speed); markers do not count (R-05).</summary>
public sealed class SheetBlocksTests
{
    public static TheoryData<string> FixtureSheets => ["zomeravond", "avondlied", "radio", "inject-en", "inject-nl"];

    [Theory]
    [MemberData(nameof(FixtureSheets))]
    public void FixtureSheets_SplitAsExpected(string key)
    {
        var sheet = AssistFixtures.Expected().GetProperty("sheets").GetProperty(key);
        var text = AssistFixtures.Read(sheet.GetProperty("file").GetString()!);
        var expected = sheet.GetProperty("blocks").EnumerateArray().Select(b => (b[0].GetInt32(), b[1].GetInt32())).ToList();

        SheetBlocks.Split(StemStudio.Domain.Lyrics.LyricsNormaliser.Normalize(text)).Select(b => (b.FirstLine, b.LastLine)).ShouldBe(expected);
    }

    [Fact]
    public void LongSection_IsSplitAfterEightLyricLines()
    {
        var text = "[verse]\n" + string.Join('\n', Enumerable.Range(1, 10).Select(i => $"line {i}"));

        var blocks = SheetBlocks.Split(text);

        blocks.Select(b => (b.Index, b.FirstLine, b.LastLine)).ShouldBe([(0, 1, 9), (1, 10, 11)]);
    }

    [Fact]
    public void ShortSections_AreMergedIntoBlocksOfUpToEightLyricLines()
    {
        var text = "[verse]\na\nb\nc\n\n[chorus]\nd\ne\nf\ng\nh\n\n[bridge]\ni\nj";

        var blocks = SheetBlocks.Split(text);

        blocks.Select(b => (b.Index, b.FirstLine, b.LastLine)).ShouldBe([(0, 1, 11), (1, 13, 15)]);
        SheetBlocks.Split("a\nb\n\nc\nd\n\ne").Select(b => (b.FirstLine, b.LastLine)).ShouldBe([(1, 7)]);
    }

    [Fact]
    public void PerformanceSheet_HasFiveBlocksOfEightLines()
    {
        var blocks = SheetBlocks.Split(AssistFixtures.Sheet("p1-havenlicht.nl.txt"));

        blocks.Count.ShouldBe(5);
        blocks[0].ShouldBe(new SheetBlock(0, 1, 9));
    }

    [Fact]
    public void MarkersOnlyAndBlankRuns_GiveNoBlocks()
    {
        SheetBlocks.Split("[verse]\n\n[chorus]").ShouldBeEmpty();
        SheetBlocks.Split("one\n\n\ntwo").Select(b => (b.FirstLine, b.LastLine)).ShouldBe([(1, 4)]);
    }

    [Fact]
    public void Range_ClipsAndDropsBlocksWithoutLyrics()
    {
        var text = AssistFixtures.Sheet("zomeravond.nl.txt");

        SheetBlocks.Split(text, lineFrom: 3, lineTo: 8).Select(b => (b.Index, b.FirstLine, b.LastLine)).ShouldBe([(0, 3, 8)]);
        SheetBlocks.Split(text, lineFrom: 6, lineTo: 7).ShouldBeEmpty();
    }

    [Fact]
    public void MaxLines_MustBePositive() => Should.Throw<ArgumentOutOfRangeException>(() => SheetBlocks.Split("a", 0));
}
