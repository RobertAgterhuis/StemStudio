using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Insights;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Insights;

/// <summary>Stem quality card of the track page (T046, US1 scenarios 1-4, FR-001..FR-004, FR-016, ui-test-ids 1.19.0).</summary>
public sealed class QualityPanelTests : PageTestContext
{
    private static readonly Guid Track = TestData.TrackId;

    private void Reports(params QualityReportDto[] answers) =>
        Api.GetQualityReportAsync(Track, Arg.Any<CancellationToken>()).Returns(answers[0], answers[1..]);

    private IRenderedComponent<QualityPanel> RenderPanel(TrackDto? track = null) =>
        Render<QualityPanel>(p => p.Add(c => c.Track, track ?? TestData.Track()));

    private Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        RenderAtLevel<QualityPanel>(level, p => p.Add(c => c.Track, TestData.Track()));

    [Fact]
    public void NeverMade_OffersTheCheck_WithoutDisclaimerOrRows()
    {
        var panel = RenderPanel();

        panel.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("none");
        var check = panel.Find("[data-testid=quality-check]");
        check.TextContent.Trim().ShouldEndWith("Check stem quality");
        check.HasAttribute("disabled").ShouldBeFalse();
        panel.FindAll("[data-testid=quality-disclaimer]").ShouldBeEmpty();
        panel.FindAll("[data-testid^=quality-row-]").ShouldBeEmpty();
        panel.FindAll("[data-testid=quality-progress]").ShouldBeEmpty();
    }

    [Fact]
    public void Check_QueuesTheReport_ShowsProgress_DisablesTheButton_AndPollsUntilReady()
    {
        Api.RequestQualityReportAsync(Track, Arg.Any<CancellationToken>()).Returns(InsightsData.Report(QualityReportStatusDto.Queued, stems: false));
        Reports(InsightsData.NoReport(), InsightsData.Report(QualityReportStatusDto.Running, 40, stems: false), InsightsData.ReadyReport());
        var panel = RenderPanel();

        panel.Find("[data-testid=quality-check]").Click();

        panel.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("queued");
        panel.Find("[data-testid=quality-check]").HasAttribute("disabled").ShouldBeTrue();
        panel.Find("[data-testid=quality-progress]").ShouldNotBeNull();
        panel.Find("[data-testid=quality-status-live]").TextContent.ShouldBe("Stem quality check queued.");
        Timer.Tick();
        panel.WaitForAssertion(() => panel.Find("[data-testid=quality-progress]").GetAttribute("aria-valuenow").ShouldBe("40"));
        Timer.Tick();
        panel.WaitForAssertion(() => panel.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("ready"));
        panel.Find("[data-testid=quality-check]").TextContent.Trim().ShouldEndWith("Check again");
        panel.FindAll("[data-testid^=quality-row-]").Count.ShouldBe(2);
    }

    [Fact]
    public void Ready_ShowsTheDisclaimer_AndEveryRatingWithWordIconAndLabel()
    {
        Reports(InsightsData.ReadyReport());

        var panel = RenderPanel();

        panel.Find("[data-testid=quality-disclaimer]").TextContent.ShouldBe(ContractLimits.QualityDisclaimer);
        panel.Find("[data-testid=quality-row-other]").GetAttribute("data-rating").ShouldBe("fair");
        var rating = panel.Find("[data-testid=quality-rating-other]");
        rating.TextContent.ShouldContain("Fair");
        rating.QuerySelector(".rzi")!.GetAttribute("aria-hidden").ShouldBe("true");
        rating.GetAttribute("role").ShouldBe("img");
        rating.GetAttribute("aria-label").ShouldBe("Other: fair, some sound of guitar is in this stem");
        panel.Find("[data-testid=quality-row-piano]").GetAttribute("data-rating").ShouldBe("not-present");
        panel.Find("[data-testid=quality-rating-piano]").TextContent.ShouldContain("Probably not in this song");
        panel.Find("[data-testid=quality-reason-piano]").TextContent.ShouldContain("what you hear is spill from other instruments");
        panel.FindAll("[data-testid^=quality-previous-]").ShouldBeEmpty();
    }

    [Fact]
    public void Beginner_SeesRatingReasonAndTip_ButNoMeasures()
    {
        Reports(InsightsData.ReadyReport());

        var page = RenderAt(ExperienceLevel.Beginner);

        page.Find("[data-testid=quality-rating-other]").ShouldNotBeNull();
        page.Find("[data-testid=quality-tip-other-try-detail-separation]").TextContent.ShouldContain("Split further");
        page.FindAll("[data-testid^=quality-bleed-], [data-testid^=quality-artefacts-], [data-testid^=quality-index-]").ShouldBeEmpty();
        page.FindAll("[data-testid=quality-analyzer-version]").ShouldBeEmpty();
        page.Find("[data-testid=hint-stem-quality]").ShouldNotBeNull();
    }

    [Fact]
    public void Advanced_AddsBleedAndArtefacts_ButNoIndices()
    {
        Reports(InsightsData.ReadyReport());

        var page = RenderAt(ExperienceLevel.Advanced);

        page.Find("[data-testid=quality-bleed-other]").TextContent.ShouldBe("Bleed: medium, mostly guitar");
        page.Find("[data-testid=quality-artefacts-other]").TextContent.ShouldBe("Artefacts: low");
        page.Find("[data-testid=quality-artefacts-piano]").TextContent.ShouldBe("Artefacts: unknown (flicker)");
        page.FindAll("[data-testid^=quality-index-], [data-testid=quality-analyzer-version]").ShouldBeEmpty();
    }

    [Fact]
    public void Expert_AddsIndicesWithReliability_AndTheAnalyzerVersion()
    {
        Reports(InsightsData.ReadyReport());

        var page = RenderAt(ExperienceLevel.Expert);

        var other = page.Find("[data-testid=quality-index-other]");
        other.TextContent.ShouldBe("Bleed index 0.19 (validated), artefact index 0.20 (validated)");
        (other.GetAttribute("data-bleed-reliability"), other.GetAttribute("data-artefact-reliability")).ShouldBe(("validated", "validated"));
        page.Find("[data-testid=quality-index-piano]").GetAttribute("data-bleed-reliability").ShouldBe("indicative");
        page.Find("[data-testid=quality-index-piano]").TextContent.ShouldContain("rough indication");
        page.Find("[data-testid=quality-analyzer-version]").TextContent.ShouldBe("Analyzer 1.0.0, measured 16 Nov 2026 10:00 UTC");
    }

    [Fact]
    public void Outdated_ShowsTheNotice()
    {
        Reports(InsightsData.ReadyReport() with { Outdated = true });

        RenderPanel().Find("[data-testid=quality-outdated]").TextContent.ShouldContain("older version");
    }

    [Fact]
    public void Failed_ShowsTheError_AndKeepsTheLastReadyStems()
    {
        Reports(InsightsData.Report(QualityReportStatusDto.Failed, 100) with { Error = "Interrupted by restart." });

        var panel = RenderPanel();

        panel.Find("[data-testid=quality-error]").TextContent.ShouldContain("Interrupted by restart.");
        panel.FindAll("[data-testid^=quality-row-]").Count.ShouldBe(2);
        panel.Find("[data-testid=quality-check]").TextContent.Trim().ShouldEndWith("Check again");
    }

    [Fact]
    public void FailedWithoutText_ShowsAGenericError()
    {
        Reports(InsightsData.Report(QualityReportStatusDto.Failed, stems: false) with { Error = null });

        RenderPanel().Find("[data-testid=quality-error]").TextContent.ShouldContain("The quality check failed.");
    }

    [Fact]
    public void PreviousRating_IsShownOnlyWhenItDiffers()
    {
        var report = InsightsData.ReadyReport();
        Reports(report with { Stems = [report.Stems[0] with { PreviousRating = QualityRatingDto.Poor }, report.Stems[1] with { PreviousRating = QualityRatingDto.NotPresent }] });

        var panel = RenderPanel();

        panel.Find("[data-testid=quality-previous-other]").TextContent.ShouldBe("was Poor");
        panel.FindAll("[data-testid=quality-previous-piano]").ShouldBeEmpty();
    }

    [Fact]
    public void DetailStems_AreRatedUnderTheirParent()
    {
        Reports(InsightsData.WithStems(
            InsightsData.Stem("drums", QualityRatingDto.Good),
            InsightsData.Stem("drums.kick", QualityRatingDto.Good, "drums"),
            InsightsData.Stem("drums.rest", QualityRatingDto.Poor, "drums")));

        var panel = RenderPanel();

        var drums = panel.Find("[data-testid=quality-row-drums]");
        drums.QuerySelector("[data-testid=quality-row-drums-kick]").ShouldNotBeNull();
        drums.QuerySelector("[data-testid=quality-row-drums-rest]")!.GetAttribute("data-rating").ShouldBe("poor");
        panel.Find("[data-testid=quality-rating-drums-kick]").GetAttribute("aria-label").ShouldStartWith("Kick (drums): good");
        panel.FindAll("[data-testid^=quality-row-]").Count.ShouldBe(5);
    }

    [Fact]
    public void LoadError_IsShown_AndTheCheckStaysAvailable()
    {
        Api.GetQualityReportAsync(Track, Arg.Any<CancellationToken>()).Returns<QualityReportDto>(_ => throw Problem(HttpStatusCode.ServiceUnavailable, "Worker unavailable"));

        var panel = RenderPanel();

        panel.Find("[data-testid=quality-error]").TextContent.ShouldContain("Worker unavailable");
        panel.Find("[data-testid=quality-check]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void CheckConflict_ShowsTheProblem()
    {
        Api.RequestQualityReportAsync(Track, Arg.Any<CancellationToken>())
            .Returns<QualityReportDto>(_ => throw Problem(HttpStatusCode.Conflict, "Quality report already running", "A quality report for this track is already being made."));
        var panel = RenderPanel();

        panel.Find("[data-testid=quality-check]").Click();

        panel.Find("[data-testid=quality-error]").TextContent.ShouldContain("already");
    }

    [Fact]
    public void AutomaticStart_SeenOnTheTrack_ReloadsAndPolls()
    {
        Reports(InsightsData.ReadyReport(), InsightsData.Report(QualityReportStatusDto.Running, 10), InsightsData.ReadyReport());
        var panel = RenderPanel();

        var running = TestData.Track() with { Quality = new QualityBadgeDto { Status = QualityReportStatusDto.Running, Summary = InsightsData.ReadyReport().Summary, Outdated = false } };
        panel.Render(p => p.Add(c => c.Track, running));

        panel.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("running");
        Timer.Tick();
        panel.WaitForAssertion(() => panel.Find("[data-testid=quality-panel]").GetAttribute("data-status").ShouldBe("ready"));
    }

    [Fact]
    public void Accessibility_RegionIsLabelled_ControlsAreNamed_TipsArePlainLists()
    {
        Reports(InsightsData.ReadyReport());

        var panel = RenderAt(ExperienceLevel.Expert);

        var region = panel.Find("[data-testid=quality-panel]");
        region.LocalName.ShouldBe("section");
        panel.Find($"#{region.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("Stem quality");
        region.GetAttribute("data-help-id").ShouldBe("quality-panel");
        panel.Find("[data-testid=quality-status-live]").GetAttribute("aria-live").ShouldBe("polite");
        panel.FindAll("[role=listbox], [role=option]").ShouldBeEmpty();
        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(region);
    }
}
