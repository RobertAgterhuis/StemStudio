using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Domain.Tests.Lyrics.Assist;

/// <summary>023 T012: re-anchoring on save (FR-008): same place, same line, +-2 lines via the line diff, else stale.</summary>
public sealed class AnchorResolverTests
{
    private const string Old = "the cat sat\non the mat\nin the hat";

    [Fact]
    public void SamePlace_IsKept()
    {
        var anchor = new Anchor(2, 7, 3, "mat");

        new AnchorResolver(Old, Old + "\nmore").Resolve(anchor).ShouldBe(anchor);
    }

    [Fact]
    public void SameLine_UniqueOccurrenceMoves()
    {
        var resolved = new AnchorResolver(Old, "the cat sat\non the big mat\nin the hat").Resolve(new Anchor(2, 7, 3, "mat"));

        resolved.ShouldBe(new Anchor(2, 11, 3, "mat"));
    }

    [Fact]
    public void InsertedLinesAbove_FollowTheLineDiff()
    {
        var resolved = new AnchorResolver(Old, "new first\nnew second\nthe cat sat\non the mat\nin the hat").Resolve(new Anchor(2, 7, 3, "mat"));

        resolved.ShouldBe(new Anchor(4, 7, 3, "mat"));
    }

    [Fact]
    public void EditedLineAfterInsert_IsFoundWithinTwoLines()
    {
        var resolved = new AnchorResolver(Old, "intro\nthe cat sat\non a mat\nin the hat").Resolve(new Anchor(2, 7, 3, "mat"));

        resolved.ShouldBe(new Anchor(3, 5, 3, "mat"));
    }

    [Fact]
    public void WithinWindow_OnNeighbourLine()
    {
        var resolved = new AnchorResolver("aaa\nbbb word\nccc", "aaa\nbbb\nccc word").Resolve(new Anchor(2, 4, 4, "word"));

        resolved.ShouldBe(new Anchor(3, 4, 4, "word"));
    }

    [Theory]
    [InlineData("the cat sat\non the rug\nin the hat")]
    [InlineData("the cat sat\non the rug\nmat mat")]
    public void GoneOrAmbiguous_IsStale(string newText) =>
        new AnchorResolver(Old, newText).Resolve(new Anchor(2, 7, 3, "mat")).ShouldBeNull();

    [Fact]
    public void LineOutOfRange_IsStale() => new AnchorResolver(Old, Old).Resolve(new Anchor(9, 0, 1, "x")).ShouldBeNull();

    [Fact]
    public void TitleAnchor_IsKept()
    {
        var title = new Anchor(0, 0, 4, "Song");

        new AnchorResolver(Old, "other").Resolve(title).ShouldBe(title);
    }

    [Fact]
    public void InsertionPoint_MovesWithItsLineOrGoesStale()
    {
        var anchor = new Anchor(2, 10, 0, string.Empty);

        new AnchorResolver(Old, "x\nthe cat sat\non the mat\nin the hat").Resolve(anchor).ShouldBe(new Anchor(3, 10, 0, string.Empty));
        new AnchorResolver(Old, "the cat sat\nin the hat").Resolve(anchor).ShouldBeNull();
    }

    [Fact]
    public void LineDiff_CountsChangedLines()
    {
        LineDiff.ChangedLines(["a", "b", "c"], ["a", "x", "c", "d"]).ShouldBe(2);
        LineDiff.Map(["a", "b"], ["b", "a"]).Count(index => index is not null).ShouldBe(1);
    }

    [Fact]
    public void Anchor_MatchesOnlyTheExactText()
    {
        new Anchor(1, 4, 3, "cat").MatchesIn("the cat").ShouldBeTrue();
        new Anchor(1, 4, 3, "cot").MatchesIn("the cat").ShouldBeFalse();
        new Anchor(1, 5, 3, "cat").MatchesIn("the cat").ShouldBeFalse();
        new Anchor(1, 4, 2, "cat").MatchesIn("the cat").ShouldBeFalse();
        new Anchor(1, 9, 0, string.Empty).MatchesIn("the cat").ShouldBeFalse();
    }
}
