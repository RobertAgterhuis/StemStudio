using Bunit;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Shared;
using StemStudio.Web.Services;
using StemStudio.Web.Tests.Fakes;
using ErrorEventArgs = Microsoft.AspNetCore.Components.Web.ErrorEventArgs;

namespace StemStudio.Web.Tests.Artwork;

/// <summary>Cover, thumbnail and placeholder (005 T028, FR-006, US1-1..5, ui-test-ids 1.4.0).</summary>
public sealed class TrackArtworkTests : PageTestContext
{
    private const string Title = "Never Gonna Give You Up";

    private IRenderedComponent<TrackArtwork> RenderArtwork(TrackDto track, ArtworkVariant variant = ArtworkVariant.Cover, string? helpId = "artwork") =>
        Render<TrackArtwork>(p => p
            .Add(c => c.Track, track)
            .Add(c => c.Variant, variant)
            .Add(c => c.TestId, "track-cover")
            .Add(c => c.HelpId, helpId));

    [Fact]
    public void Cover_IsALazyImageWithAltTextSizeAndSource()
    {
        var img = RenderArtwork(ArtworkData.WithArtwork()).Find("[data-testid=track-cover]");

        img.TagName.ShouldBe("IMG");
        img.GetAttribute("src").ShouldBe(ArtworkData.CoverUrl());
        img.GetAttribute("alt").ShouldBe($"Cover of {Title}");
        img.GetAttribute("loading").ShouldBe("lazy");
        img.GetAttribute("decoding").ShouldBe("async");
        img.GetAttribute("width").ShouldBe("600");
        img.GetAttribute("height").ShouldBe("600");
        img.GetAttribute("data-artwork-source").ShouldBe("spotify");
        img.GetAttribute("data-help-id").ShouldBe("artwork");
    }

    [Fact]
    public void Thumb_UsesTheThumbnailUrlAndSize()
    {
        var img = RenderArtwork(ArtworkData.WithArtwork(), ArtworkVariant.Thumb, null).Find("img");

        img.GetAttribute("src").ShouldBe(ArtworkData.ThumbUrl());
        img.GetAttribute("width").ShouldBe("160");
        img.GetAttribute("height").ShouldBe("160");
        img.HasAttribute("data-help-id").ShouldBeFalse();
    }

    [Theory]
    [InlineData(ArtworkSourceDto.User, "user")]
    [InlineData(ArtworkSourceDto.Spotify, "spotify")]
    [InlineData(ArtworkSourceDto.Embedded, "embedded")]
    [InlineData(ArtworkSourceDto.CoverArtArchive, "coverartarchive")]
    [InlineData(ArtworkSourceDto.Youtube, "youtube")]
    public void Source_IsTheContractValue(ArtworkSourceDto source, string expected) =>
        RenderArtwork(ArtworkData.WithArtwork(source)).Find("img").GetAttribute("data-artwork-source").ShouldBe(expected);

    [Fact]
    public void NoArtwork_ShowsThePlaceholderWithATextAlternative()
    {
        var placeholder = RenderArtwork(TestData.Track()).Find("[data-testid=track-cover]");

        placeholder.TagName.ShouldNotBe("IMG");
        placeholder.GetAttribute("data-artwork-source").ShouldBe("none");
        placeholder.GetAttribute("role").ShouldBe("img");
        placeholder.GetAttribute("aria-label").ShouldBe($"No picture for {Title}");
        placeholder.GetAttribute("data-help-id").ShouldBe("artwork");
        var initials = placeholder.QuerySelector(".ss-art-initials")!;
        initials.TextContent.ShouldBe("NG");
        initials.GetAttribute("aria-hidden").ShouldBe("true");
        placeholder.ClassList.ShouldContain($"ss-art-tone-{ArtworkDisplay.Tone(Title)}");
    }

    [Fact]
    public void ABrokenImage_FallsBackToThePlaceholder()
    {
        var page = RenderArtwork(ArtworkData.WithArtwork());

        page.Find("img").TriggerEvent("onerror", new ErrorEventArgs());

        page.FindAll("img").ShouldBeEmpty();
        page.Find("[data-testid=track-cover]").GetAttribute("data-artwork-source").ShouldBe("none");
    }

    [Fact]
    public void ANewPictureVersion_IsTriedAgainAfterABrokenOne()
    {
        var page = RenderArtwork(ArtworkData.WithArtwork());
        page.Find("img").TriggerEvent("onerror", new ErrorEventArgs());

        page.Render(p => p.Add(c => c.Track, TestData.Track() with { Artwork = ArtworkData.Artwork(ArtworkSourceDto.User, "aaaaaaaaaaaa") }));

        page.Find("img").GetAttribute("src").ShouldBe(ArtworkData.CoverUrl("aaaaaaaaaaaa"));
        page.Find("img").GetAttribute("data-artwork-source").ShouldBe("user");
    }

    [Theory]
    [InlineData("Hero of the Day", "HO")]
    [InlineData("Metallica", "M")]
    [InlineData("  monkeys   spinning monkeys ", "MS")]
    [InlineData("42 things", "4T")]
    [InlineData("(Live) - demo", "LD")]
    [InlineData("--", "?")]
    [InlineData("", "?")]
    public void Initials_AreTheFirstLettersOfTheFirstTwoWords(string title, string expected) =>
        ArtworkDisplay.Initials(title).ShouldBe(expected);

    [Fact]
    public void Tone_IsAStableStemColourOfTheTitle()
    {
        ArtworkDisplay.Tone("Hero of the Day").ShouldBe(ArtworkDisplay.Tone("Hero of the Day"));
        StemStudio.Contracts.StemNames.SixStem.ShouldContain(ArtworkDisplay.Tone("Hero of the Day"));
        StemStudio.Contracts.StemNames.SixStem.ShouldContain(ArtworkDisplay.Tone(string.Empty));
        new[] { "a", "b", "c", "d", "e", "f", "g", "h" }.Select(ArtworkDisplay.Tone).Distinct().Count().ShouldBeGreaterThan(1);
    }

    [Fact]
    public void SourceName_IsNoneWithoutArtwork() => ArtworkDisplay.SourceName(null).ShouldBe("none");
}
