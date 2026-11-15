using Bunit;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages.Lyrics;

/// <summary>Where the lyrics show up outside their card (006 T046/T047): the track page, help and the legal notice.</summary>
public sealed class LyricsPlacementTests : TrackDetailTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    [Fact]
    public void CompletedTrack_HasTheLyricsCard()
    {
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());

        var page = RenderPage(TestData.Track());

        page.WaitForAssertion(() => page.Find("[data-testid=lyrics-status]").GetAttribute("data-status").ShouldBe("found"));
    }

    [Fact]
    public void RunningTrack_HasNoLyricsCardYet()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Running));

        page.FindAll("[data-testid=lyrics-section]").ShouldBeEmpty();
    }

    [Fact]
    public void LegalNotice_MentionsLyrics()
    {
        var notice = Render<Import>().Find("[data-testid=legal-notice]");

        notice.TextContent.ShouldContain("Lyrics are fetched from LRCLIB");
        notice.TextContent.ShouldContain("never exported");
    }

    [Theory]
    [InlineData("lyrics-sources", null)]
    [InlineData("lyrics-align", null)]
    public void LyricsTopics_ExplainTheirMarkers(string id, string? feature)
    {
        var topic = Catalog.Topic(id).ShouldNotBeNull();

        Catalog.TopicForAnchor(id)!.Id.ShouldBe(id);
        (topic.Area, topic.Feature, topic.Reviewed).ShouldBe(("tracks", feature, "006"));
    }

    [Fact]
    public void LyricsSourcesTopic_SaysWhatIsSentAndTheCopyright()
    {
        var body = Catalog.Topic("lyrics-sources")!.Markdown;

        body.ShouldContain("LRCLIB");
        body.ShouldContain("title, artist, album and duration");
        body.ShouldContain("personal practice");
    }

    [Fact]
    public void LyricsAlignTopic_ExplainsDevicesAndTheModelDownload()
    {
        var body = Catalog.Topic("lyrics-align")!.Markdown;

        body.ShouldContain("GPU");
        body.ShouldContain("CPU");
        body.ShouldContain("1.6 GB");
    }
}
