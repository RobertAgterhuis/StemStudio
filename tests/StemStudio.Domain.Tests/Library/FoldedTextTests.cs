using StemStudio.Domain.Library;

namespace StemStudio.Domain.Tests.Library;

/// <summary>022 T010: folding with the index map used for search highlights (research R-07).</summary>
public sealed class FoldedTextTests
{
    [Fact]
    public void Fold_PlainText_MapsOneToOne()
    {
        var folded = LibraryKey.Fold("Abc");

        folded.Value.ShouldBe("abc");
        folded.ToOriginal(1, 2).ShouldBe((1, 2));
    }

    [Fact]
    public void Fold_SharpS_ExpandsAndMapsBack()
    {
        var folded = LibraryKey.Fold("Straße");

        folded.Value.ShouldBe("strasse");
        var start = folded.Value.IndexOf("ss", StringComparison.Ordinal);
        folded.ToOriginal(start, 2).ShouldBe((4, 1));
        folded.ToOriginal(start + 1, 2).ShouldBe((4, 2));
    }

    [Fact]
    public void Fold_Ligature_MapsToOneOriginalCharacter()
    {
        var folded = LibraryKey.Fold("ﬁsh");

        folded.Value.ShouldBe("fish");
        folded.ToOriginal(0, 1).ShouldBe((0, 1));
        folded.ToOriginal(1, 3).ShouldBe((0, 3));
    }

    [Fact]
    public void Fold_CombiningMark_IsInsideTheHighlightOfItsLetter()
    {
        var folded = LibraryKey.Fold("Motél");

        folded.Value.ShouldBe("motel");
        folded.ToOriginal(3, 1).ShouldBe((3, 2));
        folded.ToOriginal(3, 2).ShouldBe((3, 3));
    }

    [Fact]
    public void Fold_Umlaut_MapsBack()
    {
        var folded = LibraryKey.Fold("Motörhead");

        folded.Value.ShouldBe("motorhead");
        folded.ToOriginal(0, 5).ShouldBe((0, 5));
    }

    [Fact]
    public void Fold_Whitespace_CollapsesAndTrims()
    {
        var folded = LibraryKey.Fold("  a  \t b  ");

        folded.Value.ShouldBe("a b");
        folded.ToOriginal(2, 1).ShouldBe((7, 1));
    }

    [Fact]
    public void Fold_SurrogatePair_KeepsBothUnits()
    {
        var folded = LibraryKey.Fold("a😀b");

        folded.Value.ShouldBe("a😀b");
        folded.ToOriginal(1, 2).ShouldBe((1, 2));
        folded.ToOriginal(3, 1).ShouldBe((3, 1));
    }

    [Fact]
    public void Fold_Empty_IsEmpty() => LibraryKey.Fold(string.Empty).Value.ShouldBeEmpty();

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    [InlineData(2, 5)]
    public void ToOriginal_OutOfRange_Throws(int start, int length) =>
        Should.Throw<ArgumentOutOfRangeException>(() => LibraryKey.Fold("abc").ToOriginal(start, length));
}
