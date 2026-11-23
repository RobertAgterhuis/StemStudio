using Bunit;
using NSubstitute;
using StemStudio.Contracts.Insights;
using StemStudio.Web.Components.Insights;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Insights;

/// <summary>Rating badge of the mixer strips (T046, ui-test-ids quality-badge-*, R20-1).</summary>
public sealed class QualityBadgeTests : StemStudio.Web.Tests.Mixer.MixerTestContext
{
    private static QualityBadgeDto Badge() =>
        new() { Status = QualityReportStatusDto.Ready, Summary = InsightsData.ReadyReport().Summary, Outdated = false };

    [Fact]
    public void Badge_ShowsWordAndIcon_AndLinksToTheTrackPageRow()
    {
        var stem = InsightsData.Stem("drums.kick", QualityRatingDto.Poor, "drums");

        var badge = Render<QualityBadge>(p => p.Add(c => c.TrackId, TestData.TrackId).Add(c => c.Stem, stem)).Find("[data-testid=quality-badge-drums-kick]");

        badge.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}#quality-row-drums-kick");
        badge.GetAttribute("data-rating").ShouldBe("poor");
        badge.TextContent.ShouldContain("Poor");
        badge.QuerySelector(".rzi")!.TextContent.ShouldBe("warning");
        badge.GetAttribute("aria-label").ShouldBe("Quality of Kick (drums): Poor. Open the stem quality report");
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(badge.ParentElement!);
    }

    [Fact]
    public void Mixer_ShowsABadgePerRatedStrip_WhenTheTrackHasAReport()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { Quality = Badge() });
        Api.GetQualityReportAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InsightsData.ReadyReport());

        var mixer = RenderMixer();

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=strip-piano] [data-testid=quality-badge-piano]").GetAttribute("data-rating").ShouldBe("not-present"));
        mixer.Find("[data-testid=strip-other] [data-testid=quality-badge-other]").ShouldNotBeNull();
        mixer.FindAll("[data-testid=quality-badge-vocals]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Mixer_DetailStrips_GetTheirBadge()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack() with { Quality = Badge() });
        Api.GetQualityReportAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns(InsightsData.WithStems(InsightsData.Stem("drums.kick", QualityRatingDto.Good, "drums")));
        var mixer = RenderMixer();

        await mixer.Find("[data-testid=expand-drums]").ClickAsync(new());

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=group-drums] [data-testid=quality-badge-drums-kick]").ShouldNotBeNull());
    }

    [Fact]
    public void Mixer_WithoutAReport_DoesNotAsk_AndAFailingReadShowsNoBadges()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());

        var mixer = RenderMixer();

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=strip-piano]"));
        Api.DidNotReceive().GetQualityReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        mixer.FindAll("[data-testid^=quality-badge-]").ShouldBeEmpty();
    }

    [Fact]
    public void Mixer_ReadError_ShowsNoBadges()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track() with { Quality = Badge() });
        Api.GetQualityReportAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns<QualityReportDto>(_ => throw Problem(System.Net.HttpStatusCode.NotFound, "Not found"));

        var mixer = RenderMixer();

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=strip-piano]"));
        mixer.FindAll("[data-testid^=quality-badge-]").ShouldBeEmpty();
    }

    [Fact]
    public void InsightsCss_IsLoaded_UsesTokensOnly_AndKeepsTargetsAndFocus()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/insights.css");

        RepoPaths.ReadWeb("Components/App.razor").ShouldContain("\"insights\"");
        css.ShouldContain("min-height: var(--ss-target-min)");
        css.ShouldContain(".ss-quality-badge:focus-visible");
        System.Text.RegularExpressions.Regex.IsMatch(css, @"#[0-9a-fA-F]{3,8}\b|rgb\(|hsl\(").ShouldBeFalse();
    }

    [Fact]
    public void Badge_WrapsInsideANarrowStrip_AsARoundedBox()
    {
        // 020 integration (visual run): "Probably not in this song" wraps to four lines in a 6-stem mixer strip; with the
        // pill radius the border became a circle and the words spilled out of it.
        var css = RepoPaths.ReadWeb("wwwroot/css/components/insights.css");
        var badge = System.Text.RegularExpressions.Regex.Match(css, @"\.ss-quality-badge \{[^}]*\}").Value;

        badge.ShouldContain("border-radius: var(--ss-radius-control)");
        badge.ShouldContain("max-inline-size: 100%");
        badge.ShouldContain("flex-wrap: wrap"); // the icon takes its own line before a word spills past the border
        badge.ShouldNotContain("--ss-radius-pill");
    }
}
