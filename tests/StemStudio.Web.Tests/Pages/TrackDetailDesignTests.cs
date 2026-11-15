using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components.Design;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Pages;

/// <summary>Track detail in the design system (T031, FR-005, FR-007): split visual, ribbon, stem tabs, ids unchanged.</summary>
public sealed class TrackDetailDesignTests : TrackDetailTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, TrackDto track)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(track);
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    [Theory]
    [InlineData(TrackStatusDto.Queued, "idle")]
    [InlineData(TrackStatusDto.Downloading, "running")]
    [InlineData(TrackStatusDto.Separating, "running")]
    [InlineData(TrackStatusDto.Analysing, "running")]
    [InlineData(TrackStatusDto.Completed, "done")]
    [InlineData(TrackStatusDto.Failed, "idle")]
    public void SplitProgress_FollowsTheTrackStatus_NextToTheAccessibleProgressBar(TrackStatusDto status, string state)
    {
        var page = RenderPage(TestData.Track(status) with { Progress = 47 });

        var split = page.Find("[data-testid=split-progress]");
        split.GetAttribute("data-state").ShouldBe(state);
        split.GetAttribute("aria-hidden").ShouldBe("true");
        page.Find("[data-testid=track-progress]").GetAttribute("aria-valuenow").ShouldBe("47");
    }

    [Fact]
    public void Header_HasTheTitle_TheArtist_AndTheRibbon()
    {
        var page = RenderPage(TestData.Track());

        page.Find("h1[data-testid=track-title]").TextContent.ShouldBe("Never Gonna Give You Up");
        page.Find(".ss-track-artist").TextContent.ShouldBe("Rick Astley");
        page.Find("[data-testid=track-ribbon]").GetAttribute("data-stems").ShouldBe("vocals,drums,bass,guitar,piano,other");
    }

    [Fact]
    public void Ribbon_IsOneStrand_WhileProcessing()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Separating) with { Stems = [] });

        page.Find("[data-testid=track-ribbon]").GetAttribute("data-stems").ShouldBe("");
    }

    [Fact]
    public void Stems_AreColouredTabs_KeepingTheirIds()
    {
        var page = RenderPage(TestData.Track());

        var drums = page.Find("[data-testid=track-stem-drums]");
        drums.QuerySelector(".ss-stem-tab")!.GetAttribute("style").ShouldBe($"--stem-color: {StemColor.Var("drums")}");
        drums.TextContent.Trim().ShouldBe("drums");
    }

    [Fact]
    public void Facts_AndOpenMixer_AreTheCompletedSummary()
    {
        var page = RenderPage(TestData.Track());

        page.Find("dd[data-testid=track-bpm]").TextContent.ShouldBe("113.5 BPM");
        page.Find("[data-testid=open-mixer]").ClassList.ShouldContain("rz-primary");
        page.Find("[data-testid=open-mixer]").GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}/mixer");
    }

    [Theory]
    [InlineData(DetailPhaseStatusDto.Queued, "idle")]
    [InlineData(DetailPhaseStatusDto.Running, "running")]
    [InlineData(DetailPhaseStatusDto.Completed, "done")]
    [InlineData(DetailPhaseStatusDto.Failed, "idle")]
    [InlineData(DetailPhaseStatusDto.Cancelled, "idle")]
    public void DetailSplitProgress_FollowsTheDetailStatus(DetailPhaseStatusDto status, string state)
    {
        var page = RenderPage(TestData.WithDetail(TestData.Phase(status, 40, "Separating drums")));

        var split = page.Find("[data-testid=detail-section] [data-testid=detail-split-progress]");
        split.GetAttribute("data-state").ShouldBe(state);
        page.Find("[data-testid=detail-progress]").GetAttribute("aria-valuenow").ShouldBe("40");
    }

    [Fact]
    public void DetailSplitProgress_IsAbsent_BeforeTheDetailPhase()
    {
        var page = RenderPage(TestData.Track());

        page.FindAll("[data-testid=detail-split-progress]").ShouldBeEmpty();
    }

    [Fact]
    public void DetailOutcomes_ShowTheDetailStemsAsPatternedTabsOfTheParentColour()
    {
        var page = RenderPage(TestData.DetailTrack());

        var snare = page.Find("[data-testid=detail-stem-drums-snare] .ss-stem-tab");
        snare.GetAttribute("style").ShouldBe($"--stem-color: {StemColor.Var("drums")}");
        snare.ClassList.ShouldContain("ss-pattern-dashed");
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void LimitationNote_IsVisibleAtEveryLevel(ExperienceLevel level)
    {
        var page = RenderAt(level, TestData.DetailTrack());

        page.Find("[data-testid=detail-limitation-help]").TextContent.ShouldContain("Detail separation is experimental");
    }

    [Fact]
    public void ProcessingCard_IsASection_WithAHeading()
    {
        var page = RenderPage(TestData.Track(TrackStatusDto.Separating));

        page.Find("section.ss-processing h2").TextContent.ShouldBe("Processing");
    }

    [Fact]
    public void LoadError_IsADesignedErrorState()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns<TrackDto>(_ => throw Problem(System.Net.HttpStatusCode.NotFound, "Track does not exist."));

        var page = Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));

        page.Find("[data-testid=track-load-error]").ClosestElement(".ss-state-error").ShouldNotBeNull();
    }
}
