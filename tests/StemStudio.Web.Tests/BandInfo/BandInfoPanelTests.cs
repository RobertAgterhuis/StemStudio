using System.Net;
using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.BandInfo;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.BandInfo;

/// <summary>Band info panel per status and level (005 T041, FR-008, FR-009, FR-015, ui-test-ids 1.4.0).</summary>
public sealed class BandInfoPanelTests : PageTestContext
{
    private static readonly string[] AdvancedFacts = ["country", "genres", "members", "website", "sources"];

    private readonly List<BandInfoDto> _changes = [];
    private bool _closed;

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderPanel(ExperienceLevel level, BandInfoDto first, params BandInfoDto[] next)
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(first, next);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<BandInfoPanel>(b => b
                .Add(x => x.Track, TestData.Track())
                .Add(x => x.OnChanged, info => _changes.Add(info))
                .Add(x => x.OnClose, () => _closed = true)));
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderPanel(BandInfoDto first, params BandInfoDto[] next) =>
        RenderPanel(ExperienceLevel.Advanced, first, next);

    private static IElement Fact(IRenderedComponent<CascadingValue<ExperienceContext>> page, string name) => page.Find($"[data-testid=band-info-{name}]");

    private static bool Has(IRenderedComponent<CascadingValue<ExperienceContext>> page, string id) => page.FindAll($"[data-testid={id}]").Count > 0;

    [Fact]
    public void Panel_IsALabelledRegionWithTheHelpMarker()
    {
        var panel = RenderPanel(ArtworkData.Found()).Find("[data-testid=band-info-panel]");

        panel.TagName.ShouldBe("ASIDE");
        panel.GetAttribute("aria-labelledby").ShouldBe("band-info-heading");
        panel.GetAttribute("data-help-id").ShouldBe("band-info");
        panel.QuerySelector("#band-info-heading")!.TextContent.ShouldBe("Band info");
    }

    [Theory]
    [InlineData(MetadataStatusDto.Searching)]
    [InlineData(MetadataStatusDto.Pending)]
    [InlineData(MetadataStatusDto.None)]
    public void WhileSearching_ShowsASkeletonAndPollsUntilFound(MetadataStatusDto status)
    {
        var page = RenderPanel(ArtworkData.Status(status), ArtworkData.Found());
        Fact(page, "status").GetAttribute("data-status").ShouldBe(status.ToString().ToLowerInvariant());
        Fact(page, "status").TextContent.ShouldContain("Searching for band information");
        page.FindAll(".ss-skeleton").ShouldNotBeEmpty();
        Has(page, "band-info-name").ShouldBeFalse();

        Timer.Tick();

        page.WaitForAssertion(() => Fact(page, "name").TextContent.ShouldBe("The Fixture Band"));
        Fact(page, "status").GetAttribute("data-status").ShouldBe("found");
        page.WaitForAssertion(() => _changes.ShouldHaveSingleItem().Status.ShouldBe(MetadataStatusDto.Found));
    }

    [Fact]
    public void Found_ShowsEveryFactAtAdvanced()
    {
        var page = RenderPanel(ArtworkData.Found());

        Fact(page, "name").TextContent.ShouldBe("The Fixture Band");
        Fact(page, "type").TextContent.ShouldBe("Group");
        Fact(page, "years").TextContent.ShouldBe("2001 - present");
        Fact(page, "country").TextContent.ShouldBe("Netherlands");
        Fact(page, "genres").TextContent.ShouldBe("synth-pop, test rock");
        Fact(page, "members").TextContent.ShouldContain("Ada Sample (since 2001)");
        Fact(page, "members").TextContent.ShouldContain("Bo Waveform (2001 - 2015)");
        Fact(page, "members").TextContent.ShouldContain("Cy Decibel");
        Fact(page, "website").QuerySelector("a")!.GetAttribute("href").ShouldBe("https://example.org/the-fixture-band");
    }

    [Fact]
    public void Found_ShowsSummaryAlbumAndOnlyTheCorrection()
    {
        var page = RenderPanel(ArtworkData.Found());

        Fact(page, "summary").TextContent.ShouldContain("fictional group invented for the StemStudio tests");
        page.Find("[data-testid=band-info-album]").TextContent.ShouldBe("Synthetic Sessions (2020)");
        Has(page, "band-info-retry").ShouldBeFalse();
        Has(page, "band-info-correct").ShouldBeTrue();
        _changes.ShouldBeEmpty();
    }

    [Fact]
    public void Found_SourcesNameEveryServiceWithItsLicence()
    {
        var sources = Fact(RenderPanel(ArtworkData.Found()), "sources");

        sources.TextContent.ShouldContain("Data:");
        foreach (var name in new[] { "MusicBrainz", "Wikidata", "Wikipedia", "Cover Art Archive", "CC0" })
        {
            sources.TextContent.ShouldContain(name);
        }

        sources.QuerySelectorAll("a").Select(a => a.GetAttribute("href")).ShouldContain("https://musicbrainz.org/recording/1");
    }

    [Fact]
    public void Attribution_HasTheThreeLinksAndTheUnmodifiedNote()
    {
        var attribution = Fact(RenderPanel(ArtworkData.Found()), "attribution");

        var links = attribution.QuerySelectorAll("a").ToDictionary(a => a.TextContent, a => a);
        links["The Fixture Band"].GetAttribute("href").ShouldBe(ArtworkData.Summary().PageUrl);
        links["Wikipedia contributors"].GetAttribute("href").ShouldBe(ArtworkData.Summary().HistoryUrl);
        links["CC BY-SA 4.0"].GetAttribute("href").ShouldBe(WikipediaSummaryDto.CcBySa40Url);
        links.Values.ShouldAllBe(a => a.GetAttribute("rel") == "noopener noreferrer" && a.GetAttribute("target") == "_blank");
        attribution.TextContent.ShouldContain("Excerpt, unmodified");
    }

    [Fact]
    public void Person_HasNoMembersRow_AndAnEndedArtistShowsBothYears()
    {
        var person = ArtworkData.Artist(ArtistTypeDto.Person) with { EndYear = 2010, Summary = null, Country = "ZZ", Genres = [] };

        var page = RenderPanel(ArtworkData.Found(person));

        Has(page, "band-info-members").ShouldBeFalse();
        Has(page, "band-info-summary").ShouldBeFalse();
        Has(page, "band-info-attribution").ShouldBeFalse();
        Has(page, "band-info-genres").ShouldBeFalse();
        Fact(page, "type").TextContent.ShouldBe("Person");
        Fact(page, "years").TextContent.ShouldBe("2001 - 2010");
        Fact(page, "country").TextContent.ShouldBe("ZZ");
    }

    [Fact]
    public void UnknownFacts_AreNotRendered()
    {
        var bare = new ArtistInfoDto { Mbid = Guid.NewGuid(), Name = "Solo" };

        var page = RenderPanel(ArtworkData.Found(bare) with { Recording = new RecordingInfoDto("Song"), Sources = [] });

        Fact(page, "name").TextContent.ShouldBe("Solo");
        foreach (var id in new[] { "type", "years", "country", "website", "members", "sources", "album" })
        {
            Has(page, $"band-info-{id}").ShouldBeFalse(id);
        }
    }

    [Fact]
    public void AlbumWithoutYear_ShowsTheAlbumOnly() =>
        RenderPanel(ArtworkData.Found() with { Recording = new RecordingInfoDto("Song", "Load") }).Find("[data-testid=band-info-album]").TextContent.ShouldBe("Load");

    [Fact]
    public void Beginner_SeesNameTypeYearsAndSummaryOnly()
    {
        var page = RenderPanel(ExperienceLevel.Beginner, ArtworkData.Found());

        foreach (var id in new[] { "name", "type", "years", "summary", "attribution", "correct" })
        {
            Has(page, $"band-info-{id}").ShouldBeTrue(id);
        }

        AdvancedFacts.Where(id => Has(page, $"band-info-{id}")).ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void AdvancedAndExpert_SeeEveryFact(ExperienceLevel level)
    {
        var page = RenderPanel(level, ArtworkData.Found());

        AdvancedFacts.Where(id => !Has(page, $"band-info-{id}")).ShouldBeEmpty();
    }

    [Fact]
    public void NotFound_SaysSoAndOffersRetryAndCorrection()
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound));

        Fact(page, "status").GetAttribute("data-status").ShouldBe("notFound");
        Fact(page, "status").TextContent.ShouldContain("No band information found");
        Has(page, "band-info-retry").ShouldBeTrue();
        Has(page, "band-info-correct").ShouldBeTrue();
        Has(page, "band-info-name").ShouldBeFalse();
    }

    [Theory]
    [InlineData("MusicBrainz answered 503 three times.", "MusicBrainz answered 503 three times.")]
    [InlineData(null, "Band information could not be retrieved; try again later.")]
    public void Failed_ShowsTheReasonAndOffersRetry(string? reason, string expected)
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.Failed, reason));

        Fact(page, "status").GetAttribute("data-status").ShouldBe("failed");
        Fact(page, "status").TextContent.ShouldContain(expected);
        Has(page, "band-info-retry").ShouldBeTrue();
        Has(page, "band-info-correct").ShouldBeTrue();
    }

    [Fact]
    public void Disabled_ExplainsAndLinksToTheHelpTopicFirst()
    {
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.Disabled));

        var notice = Fact(page, "disabled");
        notice.QuerySelector("a")!.GetAttribute("href").ShouldBe("/help/online-lookups");
        notice.GetAttribute("data-help-id").ShouldBe("online-lookups");
        Fact(page, "status").TextContent.ShouldContain("Online lookups are switched off");
        Has(page, "band-info-retry").ShouldBeFalse();
        Has(page, "band-info-correct").ShouldBeFalse();
    }

    [Fact]
    public async Task Retry_StartsALookupWithTheCurrentNames_AndPollsAgain()
    {
        Api.LookupMetadataAsync(TestData.TrackId, null, Arg.Any<CancellationToken>()).Returns(ArtworkData.WithMetadata(MetadataStatusDto.Pending));
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.Failed), ArtworkData.Status(MetadataStatusDto.Searching), ArtworkData.Found());

        await page.Find("[data-testid=band-info-retry]").ClickAsync(new());

        await Api.Received(1).LookupMetadataAsync(TestData.TrackId, null, Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => Fact(page, "status").GetAttribute("data-status").ShouldBe("searching"));
        Timer.Tick();
        page.WaitForAssertion(() => Fact(page, "status").GetAttribute("data-status").ShouldBe("found"));
    }

    [Fact]
    public async Task RetryRefused_ShowsTheReason()
    {
        Api.LookupMetadataAsync(TestData.TrackId, null, Arg.Any<CancellationToken>()).Returns<TrackDto>(_ => throw Problem(HttpStatusCode.Conflict, "Lookup in progress", "A lookup is already running."));
        var page = RenderPanel(ArtworkData.Status(MetadataStatusDto.NotFound));

        await page.Find("[data-testid=band-info-retry]").ClickAsync(new());

        page.Find("[data-testid=band-info-error]").TextContent.ShouldContain("A lookup is already running.");
    }

    [Fact]
    public void LoadError_IsShownInThePanel()
    {
        Api.GetBandInfoAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns<BandInfoDto>(_ => throw Problem(HttpStatusCode.ServiceUnavailable, "Down"));

        var page = Render<BandInfoPanel>(b => b.Add(x => x.Track, TestData.Track()));

        page.Find("[data-testid=band-info-error]").TextContent.ShouldContain("Down");
        page.FindAll("[data-testid=band-info-status]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CloseButtonAndEscape_Close()
    {
        var page = RenderPanel(ArtworkData.Found());

        await page.Find("[data-testid=band-info-close]").ClickAsync(new());
        _closed.ShouldBeTrue();
        _closed = false;
        await page.Find("[data-testid=band-info-panel]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        _closed.ShouldBeTrue();
    }

    [Fact]
    public async Task OtherKeys_DoNotClose()
    {
        var page = RenderPanel(ArtworkData.Found());

        await page.Find("[data-testid=band-info-panel]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        _closed.ShouldBeFalse();
    }
}
