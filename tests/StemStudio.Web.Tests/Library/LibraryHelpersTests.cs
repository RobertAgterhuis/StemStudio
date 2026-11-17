using StemStudio.Contracts.Library;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary><see cref="LibraryLocation"/>: routes, <c>?q=</c>, <c>?view=all</c> and URL building (022 T018, research R-09).</summary>
public sealed class LibraryLocationTests
{
    [Theory]
    [InlineData("http://host/library", null, null, false)]
    [InlineData("http://host/library?view=all", null, null, true)]
    [InlineData("http://host/library/so0a1b2c3d4e5f6071", "so0a1b2c3d4e5f6071", null, false)]
    [InlineData("http://host/library/al2c3d4e5f60718293?q=sna", "al2c3d4e5f60718293", "sna", false)]
    [InlineData("http://host/library?q=metallica+bass", null, "metallica bass", false)]
    [InlineData("http://host/library?q=mot%C3%B6rhead#asset-x", null, "motörhead", false)]
    [InlineData("library/ar1b2c3d4e5f607182?view=all", "ar1b2c3d4e5f607182", null, false)]
    [InlineData("http://host/base/library/", null, null, false)]
    [InlineData("http://host/tracks", null, null, false)]
    public void Parse_ReadsFolderQueryAndView(string uri, string? folder, string? query, bool all)
    {
        var location = LibraryLocation.Parse(uri);

        (location.FolderId, location.Query, location.AllItems).ShouldBe((folder, query, all));
    }

    [Theory]
    [InlineData("ar1b2c3d4e5f607182", FolderLevelDto.Artist)]
    [InlineData("al2c3d4e5f60718293", FolderLevelDto.Album)]
    [InlineData("so0a1b2c3d4e5f6071", FolderLevelDto.Song)]
    public void Level_ComesFromTheIdPrefix(string id, FolderLevelDto level)
    {
        var location = LibraryLocation.Parse($"http://host/library/{id}");

        location.HasValidFolder.ShouldBeTrue();
        location.Level.ShouldBe(level);
    }

    [Theory]
    [InlineData("xx1b2c3d4e5f607182")]
    [InlineData("AR1B2C3D4E5F607182")]
    [InlineData("ar1b2c")]
    [InlineData("ar1b2c3d4e5f607182z")]
    public void InvalidFolderIds_HaveNoLevel(string id)
    {
        var location = LibraryLocation.Parse($"http://host/library/{id}");

        location.HasValidFolder.ShouldBeFalse();
        location.Level.ShouldBeNull();
        LibraryLocation.Root.HasValidFolder.ShouldBeFalse();
    }

    [Fact]
    public void LongQuery_IsCutAt100Characters()
    {
        var location = LibraryLocation.Parse($"http://host/library?q={new string('a', 150)}");

        location.Query!.Length.ShouldBe(100);
        LibraryLocation.Root.WithQuery(new string('b', 120)).Query!.Length.ShouldBe(100);
    }

    [Theory]
    [InlineData(null, null, false, "library")]
    [InlineData(null, null, true, "library?view=all")]
    [InlineData(null, "  ", true, "library?view=all")]
    [InlineData("so0a1b2c3d4e5f6071", null, false, "library/so0a1b2c3d4e5f6071")]
    [InlineData("so0a1b2c3d4e5f6071", "lead vocals", false, "library/so0a1b2c3d4e5f6071?q=lead%20vocals")]
    [InlineData(null, "a&b=c", false, "library?q=a%26b%3Dc")]
    public void ToUrl_BuildsRelativeLinks(string? folder, string? query, bool all, string url) =>
        new LibraryLocation(folder, query, all).ToUrl().ShouldBe(url);

    [Fact]
    public void WithQuery_SetsAndClearsTheText_AndLeavesAllItems()
    {
        var song = LibraryLocation.Parse($"http://host/library/{LibraryData.SongId}");

        song.WithQuery("snare").ToUrl().ShouldBe($"library/{LibraryData.SongId}?q=snare");
        song.WithQuery("snare").IsSearching.ShouldBeTrue();
        song.WithQuery("snare").WithQuery(" ").ShouldBe(song);
        LibraryLocation.All.WithQuery("bass").AllItems.ShouldBeFalse();
        LibraryLocation.All.WithQuery(null).AllItems.ShouldBeTrue();
    }

    [Fact]
    public void ItemUrl_PointsAtTheRowInsideTheSongFolder()
    {
        LibraryLocation.ItemUrl(LibraryData.SongId, AssetData.AssetId).ShouldBe($"library/{LibraryData.SongId}#asset-{AssetData.AssetId}");
        LibraryLocation.ItemAnchor(AssetData.AssetId).ShouldBe($"asset-{AssetData.AssetId}");
        LibraryLocation.FolderUrl(LibraryData.MetallicaId).ShouldBe($"library/{LibraryData.MetallicaId}");
    }
}

/// <summary><see cref="HighlightedText"/>: ranges, overlaps and out-of-range safety (022 T018, FR-025).</summary>
public sealed class HighlightedTextTests
{
    private static HighlightDto Mark(int start, int length, SearchFieldDto field = SearchFieldDto.Name, string? label = null) =>
        new() { Field = field, Start = start, Length = length, Label = label };

    private static string Render(IReadOnlyList<TextSegment> segments) =>
        string.Concat(segments.Select(s => s.Marked ? $"[{s.Text}]" : s.Text));

    [Fact]
    public void NoHighlights_GiveOnePlainSegment() =>
        HighlightedText.Split("Battery", [], SearchFieldDto.Name).ShouldBe([new TextSegment("Battery", false)]);

    [Fact]
    public void Ranges_MarkTheirCharacters_InTextOrder() =>
        Render(HighlightedText.Split("Metallica - bass", [Mark(12, 4), Mark(0, 3)], SearchFieldDto.Name)).ShouldBe("[Met]allica - [bass]");

    [Fact]
    public void OverlappingAndTouchingRanges_AreMerged() =>
        Render(HighlightedText.Split("abcdefgh", [Mark(1, 3), Mark(2, 3), Mark(5, 1)], SearchFieldDto.Name)).ShouldBe("a[bcdef]gh");

    [Theory]
    [InlineData(-5, 7, "[ab]cdef")]
    [InlineData(4, 50, "abcd[ef]")]
    [InlineData(10, 2, "abcdef")]
    [InlineData(2, 0, "abcdef")]
    [InlineData(2, -3, "abcdef")]
    [InlineData(int.MaxValue, int.MaxValue, "abcdef")]
    public void OutOfRange_IsClippedAndNeverThrows(int start, int length, string expected) =>
        Render(HighlightedText.Split("abcdef", [Mark(start, length)], SearchFieldDto.Name)).ShouldBe(expected);

    [Fact]
    public void OtherFields_AreIgnored() =>
        Render(HighlightedText.Split("Battery", [Mark(0, 3, SearchFieldDto.Artist)], SearchFieldDto.Song)).ShouldBe("Battery");

    [Fact]
    public void InstrumentRanges_ApplyToTheirLabelOnly()
    {
        HighlightDto[] marks = [Mark(0, 3, SearchFieldDto.Instrument, "Snare"), Mark(0, 4, SearchFieldDto.Instrument, "Lead vocals")];

        Render(HighlightedText.Split("Snare", marks, SearchFieldDto.Instrument, "Snare")).ShouldBe("[Sna]re");
        Render(HighlightedText.Split("Lead vocals", marks, SearchFieldDto.Instrument, "Lead vocals")).ShouldBe("[Lead] vocals");
    }

    [Fact]
    public void ARange_NeverSplitsASurrogatePair()
    {
        var text = "a\U0001F3B8b";

        Render(HighlightedText.Split(text, [Mark(2, 1)], SearchFieldDto.Name)).ShouldBe("a[\U0001F3B8]b");
        Render(HighlightedText.Split(text, [Mark(0, 2)], SearchFieldDto.Name)).ShouldBe("[a\U0001F3B8]b");
    }
}

/// <summary><see cref="FolderArtModel"/>: cover, mosaic 2-4, placeholder, <c>srcset</c>/<c>sizes</c> and tone (022 T018, R-05).</summary>
public sealed class FolderArtModelTests
{
    private static readonly Uri Base = new("http://api.test:8081");

    [Fact]
    public void Cover_HasThumbSrc_AndCoverInTheSrcset()
    {
        var model = FolderArtModel.From(LibraryData.Cover(), Base);

        model.Kind.ShouldBe(FolderArtKindDto.Cover);
        var picture = model.Pictures.ShouldHaveSingleItem();
        picture.Src.ShouldBe("http://api.test:8081/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b01/artwork/thumb?v=4f2a");
        picture.SrcSet.ShouldBe("http://api.test:8081/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b01/artwork/thumb?v=4f2a 160w, " +
            "http://api.test:8081/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b01/artwork/cover?v=4f2a 600w");
        (model.Size, model.Sizes, model.EmptyCells).ShouldBe((160, "160px", 0));
    }

    [Theory]
    [InlineData(2, 2)]
    [InlineData(3, 1)]
    [InlineData(4, 0)]
    [InlineData(6, 0)]
    public void Mosaic_HasTwoToFourThumbs_AndFillsEmptyCells(int images, int empty)
    {
        var model = FolderArtModel.From(LibraryData.Cover(images), Base, compact: true);

        model.Kind.ShouldBe(FolderArtKindDto.Mosaic);
        model.Pictures.Count.ShouldBe(Math.Min(images, 4));
        model.Pictures.ShouldAllBe(p => p.SrcSet == null && p.Src.EndsWith("/thumb?v=4f2a"));
        model.EmptyCells.ShouldBe(empty);
        (model.Size, model.CellSize).ShouldBe((128, 64));
    }

    [Fact]
    public void Placeholder_HasGlyphAndToneClass()
    {
        var model = FolderArtModel.From(LibraryData.Placeholder("?", 0), Base);

        model.Kind.ShouldBe(FolderArtKindDto.Placeholder);
        model.Pictures.ShouldBeEmpty();
        (model.Glyph, model.Tone, model.ToneClass).ShouldBe(("?", 0, "ss-art-tone-0"));
    }

    [Theory]
    [InlineData(11, 3)]
    [InlineData(-1, 7)]
    [InlineData(7, 7)]
    public void Tone_StaysInsideTheEightTokens(int tone, int expected) =>
        FolderArtModel.From(LibraryData.Placeholder("M", tone), Base).Tone.ShouldBe(expected);

    [Fact]
    public void BlankGlyph_BecomesAQuestionMark() =>
        FolderArtModel.From(LibraryData.Placeholder(" "), Base).Glyph.ShouldBe("?");

    [Fact]
    public void MosaicWithOneUsablePicture_IsACover_AndCoverWithoutPictures_IsAPlaceholder()
    {
        var mosaic = LibraryData.Cover(2) with { Images = [LibraryData.Image(1), new ArtImageDto { ThumbUrl = "https://evil.example/x.png", CoverUrl = "https://evil.example/y.png" }] };
        var empty = LibraryData.Cover() with { Images = [] };

        FolderArtModel.From(mosaic, Base).Kind.ShouldBe(FolderArtKindDto.Cover);
        FolderArtModel.From(empty, Base).Kind.ShouldBe(FolderArtKindDto.Placeholder);
    }

    [Theory]
    [InlineData("https://evil.example/api/tracks/x/artwork/thumb")]
    [InlineData("//evil.example/api/tracks/x")]
    [InlineData("/api/tracks/../../admin")]
    [InlineData("/api/tracks/x/artwork/thumb onerror=alert(1)")]
    [InlineData("javascript:alert(1)")]
    public void ForeignOrOddUrls_FallBackToThePlaceholder(string url)
    {
        var art = LibraryData.Cover() with { Images = [new ArtImageDto { ThumbUrl = url, CoverUrl = url }] };

        FolderArtModel.From(art, Base).Kind.ShouldBe(FolderArtKindDto.Placeholder);
    }

    [Fact]
    public void PlaceholderKind_WithImages_StaysAPlaceholder() =>
        FolderArtModel.From(LibraryData.Cover() with { Kind = FolderArtKindDto.Placeholder }, Base).Kind.ShouldBe(FolderArtKindDto.Placeholder);
}
