using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NSubstitute;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;
using StemStudio.Web.Tests.Pages;

namespace StemStudio.Web.Tests.Insights;

/// <summary>
/// The switch <c>StemStudio:QualityReport:Enabled</c> (owner decision 2026-11-23, G1 failed on real music): while it is off the
/// track page has no quality card (no check button, no tips) and the mixer no quality badges, the report is never read, the
/// page keeps its order without a gap and stays accessible; switched on everything shows as before. The help says that the
/// report is off by default and how to switch it on.
/// </summary>
public sealed class QualityReportSwitchTests : TrackDetailTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private static TrackDto Measured(TrackDto track) => track with
    {
        Quality = new QualityBadgeDto { Status = QualityReportStatusDto.Ready, Summary = InsightsData.ReadyReport().Summary, Outdated = false },
    };

    private void Switch(bool enabled)
    {
        Services.AddSingleton(Options.Create(new StemStudioWebOptions { QualityReport = { Enabled = enabled } }));
        Api.GetQualityReportAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InsightsData.ReadyReport());
    }

    [Fact]
    public void TheWebOptions_HaveTheReportOffByDefault() => new StemStudioWebOptions().QualityReport.Enabled.ShouldBeFalse();

    [Fact]
    public void Off_TheTrackPage_HasNoQualityCard_AndNeverReadsTheReport()
    {
        Switch(false);

        var page = RenderPage(Measured(TestData.Track()));

        page.WaitForAssertion(() => page.Find("[data-testid=handout-menu], #handouts-heading"));
        page.FindAll("[data-testid^=quality-]").ShouldBeEmpty();
        page.FindAll("[data-help-id=quality-panel]").ShouldBeEmpty();
        page.FindAll(".ss-quality").ShouldBeEmpty();
        page.Markup.ShouldNotContain("Stem quality");
        page.Markup.ShouldNotContain("Check stem quality");
        Api.DidNotReceive().GetQualityReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        Api.DidNotReceive().RequestQualityReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Off_TheTrackPage_KeepsItsOrder_WithoutAnEmptyCard_AndStaysAccessible()
    {
        Switch(false);

        var page = RenderPage(Measured(TestData.Track()));

        page.WaitForAssertion(() => page.Find("#handouts-heading"));
        var handouts = page.Find("#handouts-heading").Closest("section")!;
        handouts.PreviousElementSibling.ShouldNotBeNull();
        handouts.PreviousElementSibling!.ClassList.ShouldNotContain("ss-quality");
        handouts.PreviousElementSibling.InnerHtml.Trim().ShouldNotBeEmpty();
        page.FindAll("h2").Select(heading => heading.TextContent.Trim()).ShouldNotContain("Stem quality");
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find("main, div"));
    }

    [Fact]
    public void On_TheTrackPage_ShowsTheQualityCard_WithItsCheckButtonAndTips()
    {
        Switch(true);

        var page = RenderPage(Measured(TestData.Track()));

        page.WaitForAssertion(() => page.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("ready"));
        page.Find("[data-testid=quality-check]").ShouldNotBeNull();
        page.Find("[data-testid=quality-tip-action-other-try-detail-separation]").TextContent.Trim().ShouldBe("Split further");
        page.Find("#quality-heading").TextContent.ShouldBe("Stem quality");
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find("[data-testid=quality-panel]"));
    }

    [Fact]
    public void TheHelp_SaysTheReportIsOffByDefault_AndHowToSwitchItOn()
    {
        var topic = Catalog.Topic("stem-quality").ShouldNotBeNull();

        topic.Markdown.ShouldContain("switched off by default");
        topic.Markdown.ShouldContain("StemStudio:QualityReport:Enabled");
        topic.Markdown.ShouldContain("rough indication");
    }

    [Fact]
    public void TheReliabilityHelp_GivesTheOutcomeOfTheCheckOnRealMusic()
    {
        var topic = Catalog.Topic("stem-quality-reliability").ShouldNotBeNull();

        topic.Markdown.ShouldContain("MUSDB18-HQ");
        topic.Markdown.ShouldNotContain("has not run yet");
        topic.Markdown.ShouldContain("not shown");
    }
}

/// <summary>The mixer side of the quality report switch: no badges and no report read while it is off; badges while it is on.</summary>
public sealed class MixerQualitySwitchTests : StemStudio.Web.Tests.Mixer.MixerTestContext
{
    private void Switch(bool enabled)
    {
        Services.AddSingleton(Options.Create(new StemStudioWebOptions { QualityReport = { Enabled = enabled } }));
        Api.GetQualityReportAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InsightsData.ReadyReport());
    }

    private static TrackDto Measured(TrackDto track) => track with
    {
        Quality = new QualityBadgeDto { Status = QualityReportStatusDto.Ready, Summary = InsightsData.ReadyReport().Summary, Outdated = false },
    };

    [Fact]
    public void Off_TheMixer_ShowsNoBadges_AndNeverReadsTheReport()
    {
        Switch(false);
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(Measured(TestData.Track()));

        var mixer = RenderMixer();

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=strip-piano]"));
        mixer.FindAll("[data-testid^=quality-badge-]").ShouldBeEmpty();
        Api.DidNotReceive().GetQualityReportAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void On_TheMixer_ShowsTheBadges()
    {
        Switch(true);
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(Measured(TestData.Track()));

        var mixer = RenderMixer();

        mixer.WaitForAssertion(() => mixer.Find("[data-testid=strip-piano] [data-testid=quality-badge-piano]").ShouldNotBeNull());
    }
}
