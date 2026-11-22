using System.Text.RegularExpressions;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Help;

/// <summary>
/// Help content coverage against the real embedded content and markup (contracts/help-content.md 4, FR-025,
/// FR-026, SC-009). A marker without a topic, a missing anchor, a broken link or an unknown tour target fails.
/// </summary>
public sealed partial class HelpCoverageTests : PageTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private static readonly string[] Razor = Directory
        .EnumerateFiles(RepoPaths.Web, "*.razor", SearchOption.AllDirectories)
        .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
        .Select(File.ReadAllText)
        .ToArray();

    [GeneratedRegex("(?:data-help-id|HelpId)=\"([a-z0-9-]+)\"")]
    private static partial Regex Marker();

    [GeneratedRegex("@page \"([^\"]+)\"")]
    private static partial Regex PageDirective();

    [GeneratedRegex(@"\]\(route:([^)\s]+)\)")]
    private static partial Regex RouteLink();

    private static HashSet<string> Markers() => Razor.SelectMany(r => Marker().Matches(r).Select(m => m.Groups[1].Value)).ToHashSet(StringComparer.Ordinal);

    private static List<string> PageRoutes() =>
        Razor.SelectMany(r => PageDirective().Matches(r).Select(m => Regex.Replace(m.Groups[1].Value, @"\{(\w+)(:\w+)?\}", "{$1}").ToLowerInvariant()))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    [Fact]
    public void EveryMarker_HasATopic()
    {
        var missing = Markers().Where(m => Catalog.TopicForAnchor(m) is null).ToList();

        missing.ShouldBeEmpty($"markers without a topic: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryLiteralAnchor_IsPresentInTheMarkup()
    {
        var markers = Markers();
        var dynamicPrefixes = new[] { "strip-" };

        var missing = Catalog.Topics.SelectMany(t => t.Anchors.Select(a => (Topic: t.Id, Anchor: a)))
            .Where(x => !x.Anchor.EndsWith('*') && !markers.Contains(x.Anchor) && !dynamicPrefixes.Any(p => x.Anchor.StartsWith(p, StringComparison.Ordinal)))
            .Select(x => $"{x.Topic}:{x.Anchor}")
            .ToList();

        missing.ShouldBeEmpty($"anchors not found in markup: {string.Join(", ", missing)}");
    }

    [Fact]
    public void EveryPage_HasExactlyOneScreenTopic()
    {
        // The not-found page is no screen: it only links back to the app.
        foreach (var route in PageRoutes().Where(r => r != "/not-found"))
        {
            Catalog.Topics.Count(t => t.Route == route).ShouldBe(1, $"screen topics for {route}");
        }

        Catalog.Topics.Where(t => t.Route is not null).Select(t => t.Route!).ShouldBeSubsetOf(PageRoutes());
    }

    [Fact]
    public void RouteLinks_MatchAPageRoute()
    {
        var routes = PageRoutes();

        foreach (var topic in Catalog.Topics)
        {
            foreach (Match link in RouteLink().Matches(topic.Markdown))
            {
                routes.Any(r => AppRoutes.Matches(r, link.Groups[1].Value)).ShouldBeTrue($"{topic.Id}: route:{link.Groups[1].Value}");
            }
        }
    }

    [Fact]
    public void Features_AndCatalogueTopics_ResolveBothWays()
    {
        foreach (var topic in Catalog.Topics.Where(t => t.Feature is not null))
        {
            Should.NotThrow(() => FeatureCatalog.Get(topic.Feature!), topic.Id);
        }

        foreach (var feature in FeatureCatalog.All)
        {
            Catalog.Topic(feature.TopicId).ShouldNotBeNull(feature.Id);
        }
    }

    [Fact]
    public void EveryTopic_RendersWithoutScriptRawHtmlOrExternalImages()
    {
        foreach (var topic in Catalog.Topics)
        {
            var html = Catalog.Render(topic.Id).Html;
            html.ShouldNotContain("<script", Case.Insensitive, topic.Id);
            html.ShouldNotContain("<iframe", Case.Insensitive, topic.Id);
            html.ShouldNotContain("src=\"http", Case.Insensitive, topic.Id);
        }
    }

    [Fact]
    public void DesignSpecInventory_IsComplete()
    {
        string[] required =
        [
            "welcome", "levels", "journey", "tours", "preferences", "shortcuts", "legal-notice",
            "import-screen", "import-url", "import-file", "processing-device", "stem-limitations",
            "tracks-library", "track-status", "track-screen", "track-progress", "track-retry", "track-delete", "track-facts", "track-job-details",
            "detail-stems", "detail-outcomes", "detail-limitations", "detail-cancel-retry",
            "mixer-screen", "mixer-volume", "mixer-pan", "mixer-mute-solo", "mixer-meters", "mixer-master", "mixer-transport", "mixer-loop",
            "mixer-metronome", "mixer-presets", "mixer-detail-groups", "mixer-save", "mixer-numeric-entry",
            "mixes-library", "mixes-actions",
            "glossary", "stem", "detail-stem", "bpm", "beat-grid", "downbeat", "time-signature", "pan", "solo", "mute", "loop",
            "metronome-offset", "peak-meter", "clipping", "dbfs", "gpu-cpu",
        ];

        required.Where(id => Catalog.Topic(id) is null).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("pan", "mixer-pan")]
    [InlineData("solo", "mixer-mute-solo")]
    [InlineData("mute", "mixer-mute-solo")]
    [InlineData("loop", "mixer-loop")]
    [InlineData("a-b", "mixer-loop")]
    [InlineData("metronome", "mixer-metronome")]
    [InlineData("click", "mixer-metronome")]
    [InlineData("bpm", "bpm")]
    [InlineData("tempo", "bpm")]
    [InlineData("downbeat", "downbeat")]
    [InlineData("detail stems", "detail-stems")]
    [InlineData("kick", "detail-outcomes")]
    [InlineData("youtube", "import-url")]
    [InlineData("spotify", "import-url")]
    [InlineData("upload", "import-file")]
    [InlineData("gpu", "processing-device")]
    [InlineData("save", "mixer-save")]
    [InlineData("delete track", "track-delete")]
    [InlineData("shortcuts", "shortcuts")]
    [InlineData("copyright", "legal-notice")]
    public void ReferenceQueries_ReturnTheExpectedTopicFirst(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    /// <summary>The feature 006/007/008 reference queries of the E2E help spec (e2e/support/help.ts).</summary>
    [Theory]
    [InlineData("save to library", "export-dialog")]
    [InlineData("export range", "export-range")]
    [InlineData("karaoke", "karaoke")]
    [InlineData("sing along", "karaoke")]
    [InlineData("whisper", "lyrics-align")]
    [InlineData("lyrics offset", "karaoke-offset")]
    [InlineData("lrclib", "lyrics-sources")]
    [InlineData("align lyrics", "lyrics-align")]
    [InlineData("sheet music", "sheet-music")]
    [InlineData("drum notation", "sheet-music-drums")]
    [InlineData("grand staff", "sheet-music-piano")]
    public void FeatureReferenceQueries_ReturnTheExpectedTopicFirst(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    [Fact]
    public async Task TourTargets_RenderOnTheirRoute_AtEachOfTheirLevels()
    {
        foreach (var tour in Catalog.Tours)
        {
            foreach (var level in tour.Levels)
            {
                var markup = await RenderRouteAsync(tour.Route, level);
                foreach (var step in tour.Steps)
                {
                    markup.ShouldContain($"data-help-id=\"{step.Target}\"", Case.Sensitive, $"{tour.Id} at {level}: {step.Target}");
                }
            }
        }
    }

    private static async Task<string> RenderRouteAsync(string route, ExperienceLevel level)
    {
        await using var context = new RouteContext(level);
        return context.Render(route);
    }

    /// <summary>A fresh page context per route and level, with a completed detail track and a loaded mixer.</summary>
    private sealed class RouteContext : PageTestContext
    {
        public RouteContext(ExperienceLevel level)
        {
            Store.Snapshot = new(UserPreferences.Default with { Level = level, Help = HelpProgress.Empty with { WelcomeDismissed = level != ExperienceLevel.Beginner } }, true);
            Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
            Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track()]);
            Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([TestData.Summary()]);
            Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(213.04);
        }

        public string Render(string route)
        {
            var path = route == "*" ? "/" : route.Replace("{id}", TestData.TrackId.ToString(), StringComparison.Ordinal);
            Services.GetRequiredService<NavigationManager>().NavigateTo(path);
            var routes = Render<Routes>();
            routes.WaitForAssertion(() => routes.FindAll("main h1").Count.ShouldBe(1));
            return routes.Markup;
        }
    }
}
