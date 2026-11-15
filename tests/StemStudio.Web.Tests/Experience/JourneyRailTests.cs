using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Components;
using StemStudio.Web.Components.Experience;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Experience;

/// <summary>Beginner journey rail (T041, FR-015).</summary>
public sealed class JourneyRailTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level, JourneyStage stage) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<JourneyRail>(r => r.Add(x => x.Stage, stage)));

    private static string[] States(IRenderedComponent<CascadingValue<ExperienceContext>> rail) =>
        ["import", "separate", "mix", "save"].Select(s => rail.Find($"[data-testid=journey-step-{s}]").GetAttribute("data-state")!).ToArray();

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void IsBeginnerOnly(ExperienceLevel level) =>
        RenderAt(level, JourneyStage.Import).FindAll("[data-testid=journey-rail]").ShouldBeEmpty();

    [Theory]
    [InlineData(JourneyStage.Import, new[] { "current", "todo", "todo", "todo" })]
    [InlineData(JourneyStage.Separate, new[] { "done", "current", "todo", "todo" })]
    [InlineData(JourneyStage.Mix, new[] { "done", "done", "current", "todo" })]
    [InlineData(JourneyStage.Saved, new[] { "done", "done", "done", "done" })]
    public void StepStates_FollowTheStage(JourneyStage stage, string[] states) =>
        States(RenderAt(ExperienceLevel.Beginner, stage)).ShouldBe(states);

    [Fact]
    public void CurrentStep_HasAriaCurrentStep_AndTheRailIsAnOrderedList()
    {
        var rail = RenderAt(ExperienceLevel.Beginner, JourneyStage.Separate);

        rail.Find("[data-testid=journey-rail]").QuerySelector("ol").ShouldNotBeNull();
        rail.Find("[data-testid=journey-step-separate]").GetAttribute("aria-current").ShouldBe("step");
        rail.Find("[data-testid=journey-step-import]").HasAttribute("aria-current").ShouldBeFalse();
        rail.Find("[data-testid=journey-rail]").GetAttribute("data-help-id").ShouldBe("journey");
    }

    [Fact]
    public void SavedStage_HasNoCurrentStep()
    {
        RenderAt(ExperienceLevel.Beginner, JourneyStage.Saved).FindAll("[aria-current]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(TrackStatusDto.Queued, JourneyStage.Separate)]
    [InlineData(TrackStatusDto.Separating, JourneyStage.Separate)]
    [InlineData(TrackStatusDto.Failed, JourneyStage.Separate)]
    [InlineData(TrackStatusDto.Completed, JourneyStage.Mix)]
    public void TrackPage_StageFollowsTheTrackStatus(TrackStatusDto status, JourneyStage stage) =>
        JourneyStages.ForTrack(status).ShouldBe(stage);

    [Theory]
    [InlineData(false, false, JourneyStage.Mix)]
    [InlineData(true, true, JourneyStage.Mix)]
    [InlineData(true, false, JourneyStage.Saved)]
    [InlineData(false, true, JourneyStage.Mix)]
    public void Mixer_StageIsSaved_OnlyForASavedCleanMix(bool saved, bool dirty, JourneyStage stage) =>
        JourneyStages.ForMixer(saved, dirty).ShouldBe(stage);

    [Fact]
    public void ImportScreen_PutsTheRailInTheContextBar_AtBeginner()
    {
        var routes = Render<Routes>();

        routes.WaitForAssertion(() => routes.Find("header [data-testid=journey-step-import]").GetAttribute("data-state").ShouldBe("current"));
    }

    [Fact]
    public void TrackScreen_ShowsSeparateWhileProcessing()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(TrackStatusDto.Separating));
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}");

        var routes = Render<Routes>();

        routes.WaitForAssertion(() => routes.Find("header [data-testid=journey-step-separate]").GetAttribute("data-state").ShouldBe("current"));
    }

    [Fact]
    public void MixerScreen_ShowsMixCurrent()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).Returns(213.04);
        Services.GetRequiredService<NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer");

        var routes = Render<Routes>();

        routes.WaitForAssertion(() => routes.Find("header [data-testid=journey-step-mix]").GetAttribute("data-state").ShouldBe("current"));
    }

    [Fact]
    public void OtherScreens_HaveNoRail()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([]);
        Services.GetRequiredService<NavigationManager>().NavigateTo("tracks");

        var routes = Render<Routes>();

        routes.WaitForAssertion(() => routes.Find("h1").TextContent.ShouldBe("Tracks"));
        routes.FindAll("[data-testid=journey-rail]").ShouldBeEmpty();
    }
}
