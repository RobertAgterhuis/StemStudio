using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Components.Practice;

/// <summary>T082: the score panel in the mixer (FR-009..FR-013; ui-test-ids 1.13.0 score-*).</summary>
public sealed class ScorePanelTests : MixerTestContext
{
    private static readonly Guid Other = Guid.Parse("9a8b7c6d-2222-4222-8333-444455556666");

    public ScorePanelTests()
    {
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns([PracticeFlowData.TrackScore(), PracticeFlowData.TrackScore(Other, "Guitar riff", manualTempo: true)]);
        Api.GetScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.ScoreView());
        Api.GetScoreViewAsync(Other, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.ScoreView(assetId: Other));
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderOpen(ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var page = Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<MixerPage>(m => m.Add(x => x.Id, TestData.TrackId)));
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        page.Find("[data-testid=score-panel-toggle]").Click();
        return page;
    }

    private static Task Command(IRenderedComponent<CascadingValue<ExperienceContext>> page, FakeWebInterop interop, string command) =>
        page.InvokeAsync(() => interop.CommandAsync(ShortcutScope.Mixer, command));

    [Fact]
    public void Open_ListsTheScores_ShowsThePages_AndAttachesTheFollower()
    {
        var page = RenderOpen();

        page.WaitForAssertion(() => page.Find("[data-testid=score-page-1]").GetAttribute("data-score-page").ShouldBe("1"));
        var panel = page.Find("[data-testid=score-panel]");
        (panel.GetAttribute("aria-label"), panel.GetAttribute("data-asset-id"), panel.GetAttribute("data-view-status"))
            .ShouldBe(("Score", PracticeFlowData.ScoreAssetId.ToString(), "ready"));
        page.FindAll("[data-testid=score-select] option").Select(o => o.TextContent).ShouldBe(["Bass - Bass part", "Bass - Guitar riff"]);
        page.Find("[data-testid=score-page-1] img").GetAttribute("src").ShouldEndWith($"/api/assets/{PracticeFlowData.ScoreAssetId}/score-view/pages/1?v=1");
        page.Find("[data-testid=score-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Is<ScoreViewMap>(m => m.Bars.Count == 3), new ScoreFollowOptions(true)));
        page.FindAll("[data-testid=score-tempo-note]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Select_DetachesAndAttachesTheOtherScore_AndShowsTheManualTempoNote()
    {
        var page = RenderOpen();
        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>()));

        await page.InvokeAsync(() => page.Find("[data-testid=score-select]").Change(Other.ToString()));

        await Engine.Received(1).DetachScoreFollowerAsync();
        page.WaitForAssertion(() => page.Find("[data-testid=score-panel]").GetAttribute("data-asset-id").ShouldBe(Other.ToString()));
        page.Find("[data-testid=score-tempo-note]").ShouldNotBeNull();
        page.WaitForAssertion(() => Engine.Received(2).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>()));
    }

    [Fact]
    public void NoScores_ShowsTheEmptyStateWithALinkToTheSongPage()
    {
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([]);

        var page = RenderOpen();

        page.WaitForAssertion(() => page.Find("[data-testid=score-empty] a").GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}"));
    }

    [Fact]
    public void AnOlderScore_StartsTheViewJob_AndPollsWhilePreparing()
    {
        Api.GetScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>())
            .Returns(PracticeFlowData.ScoreView(ScoreViewStatusDto.None), PracticeFlowData.ScoreView(ScoreViewStatusDto.Rendering), PracticeFlowData.ScoreView());
        Api.CreateScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.ScoreView(ScoreViewStatusDto.Queued));

        var page = RenderOpen();

        page.WaitForAssertion(() => page.Find("[data-testid=score-preparing]").TextContent.ShouldBe("Preparing the score view..."));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=score-panel]").GetAttribute("data-view-status").ShouldBe("rendering"));
        Timer.Tick();
        page.WaitForAssertion(() => page.Find("[data-testid=score-page-1]").ShouldNotBeNull());
        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>()));
    }

    [Fact]
    public void AFailedViewOrAnApiError_IsShown()
    {
        Api.GetScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.ScoreView(ScoreViewStatusDto.Failed));
        var page = RenderOpen();
        page.WaitForAssertion(() => page.Find("[data-testid=score-error]").TextContent.ShouldBe("The score could not be engraved."));

        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException("The API is down."));
        var other = RenderOpen();
        other.WaitForAssertion(() => other.Find("[data-testid=score-empty]").ShouldNotBeNull());
    }

    [Fact]
    public void AViewRequestError_IsShown()
    {
        Api.GetScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException("The score view is gone."));

        var page = RenderOpen();

        page.WaitForAssertion(() => page.Find("[data-testid=score-error]").TextContent.ShouldBe("The score view is gone."));
        page.Find("[data-testid=score-panel]").GetAttribute("data-view-status").ShouldBe("none");
    }

    [Fact]
    public void AnEngineRefusal_IsShown()
    {
        Engine.AttachScoreFollowerAsync(Arg.Any<string>(), Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>())
            .ThrowsAsync(new AudioEngineException("Invalid score view: bars must be 1..2000"));

        var page = RenderOpen();

        page.WaitForAssertion(() => page.Find("[data-testid=score-error]").TextContent.ShouldBe("Invalid score view: bars must be 1..2000"));
    }

    [Fact]
    public async Task FollowToggle_AndShiftF_SetTheOptions()
    {
        var page = RenderOpen();
        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>()));

        await page.InvokeAsync(() => page.Find("[data-testid=score-follow-toggle]").Click());
        page.Find("[data-testid=score-follow-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        await Command(page, Interop, "mixer.score.follow");

        await Engine.Received(1).SetScoreFollowOptionsAsync(new ScoreFollowOptions(false));
        await Engine.Received(1).SetScoreFollowOptionsAsync(new ScoreFollowOptions(true));
    }

    [Fact]
    public async Task KeyF_TogglesThePanel_AndClosingDetaches()
    {
        var page = RenderOpen();
        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), Arg.Any<ScoreFollowOptions>()));

        await Command(page, Interop, "mixer.score.toggle");

        page.FindAll("[data-testid=score-panel]").ShouldBeEmpty();
        await Engine.Received(1).DetachScoreFollowerAsync();
        await Command(page, Interop, "mixer.score.follow");
        await Command(page, Interop, "mixer.score.toggle");
        page.Find("[data-testid=score-panel]").ShouldNotBeNull();
    }

    [Fact]
    public void TransposeNote_WhileTransposed()
    {
        Engine.PracticeChanged += Raise.Event<EventHandler<PracticeStatus>>(Engine, PracticeStatus.Initial);
        var page = RenderOpen();
        page.WaitForAssertion(() => page.Find("[data-testid=score-page-1]").ShouldNotBeNull());
        page.FindAll("[data-testid=score-transpose-note]").ShouldBeEmpty();

        page.Find("[data-testid=practice-transpose-up]").Click();

        page.WaitForAssertion(() => page.Find("[data-testid=score-transpose-note]").ShouldNotBeNull());
    }

    [Fact]
    public async Task ClickABar_Seeks_AndShiftClick_LoopsTheBarsFromTheLoopStart()
    {
        var page = RenderOpen();
        page.WaitForAssertion(() => page.FindAll(".ss-score-bar").Count.ShouldBe(3));

        await page.InvokeAsync(() => page.FindAll(".ss-score-bar")[1].Click());
        await Engine.Received(1).SeekAsync(8.1);
        await page.InvokeAsync(() => page.FindAll(".ss-score-bar")[0].Click(new MouseEventArgs { ShiftKey = true }));
        await Engine.Received().SetLoopAsync(new LoopRegionDto(6.1, 8.1, true));
        await page.InvokeAsync(() => page.FindAll(".ss-score-bar")[2].Click(new MouseEventArgs { ShiftKey = true }));

        await Engine.Received().SetLoopAsync(new LoopRegionDto(6.1, 12.1, true));
    }

    [Fact]
    public async Task ShiftClick_AtBeginner_Seeks()
    {
        var page = RenderOpen(ExperienceLevel.Beginner);
        page.WaitForAssertion(() => page.FindAll(".ss-score-bar").Count.ShouldBe(3));

        await page.InvokeAsync(() => page.FindAll(".ss-score-bar")[2].Click(new MouseEventArgs { ShiftKey = true }));

        await Engine.Received(1).SeekAsync(10.1);
        await Engine.DidNotReceive().SetLoopAsync(Arg.Any<LoopRegionDto>());
    }

    [Fact]
    public async Task BarLabel_FollowsThePosition_AndReadBarAnnouncesIt()
    {
        var page = RenderOpen();
        page.WaitForAssertion(() => page.FindAll(".ss-score-bar").Count.ShouldBe(3));

        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 9.0);
        page.WaitForAssertion(() => page.Find("[data-testid=score-bar-label]").TextContent.ShouldBe("Bar 1 of 2"));
        page.Find("[data-testid=score-bar-label]").GetAttribute("aria-live").ShouldBe("off");
        await page.InvokeAsync(() => page.Find("[data-testid=score-read-bar]").Click());
        page.Find("[data-testid=score-panel] [role=status]:not([data-testid])").TextContent.ShouldBe("Bar 1 of 2");
        await page.InvokeAsync(() => page.Find("[data-testid=score-read-bar]").Click());
        page.Find("[data-testid=score-panel] [role=status]:not([data-testid])").TextContent.Trim().ShouldBe("Bar 1 of 2");

        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 30.0);
        page.WaitForAssertion(() => page.Find("[data-testid=score-bar-label]").TextContent.ShouldBeEmpty());
        await page.InvokeAsync(() => page.Find("[data-testid=score-read-bar]").Click());
        page.Find("[data-testid=score-panel] [role=status]:not([data-testid])").TextContent.ShouldBe("Not in a bar");
    }

    [Fact]
    public async Task ReducedMotion_FromThePreferences_IsPassed()
    {
        Store.Snapshot = new(UserPreferences.Default with { Motion = MotionPreference.Reduced }, true);
        await Services.GetRequiredService<PreferencesState>().LoadAsync();

        var page = RenderOpen();

        page.WaitForAssertion(() => Engine.Received(1).AttachScoreFollowerAsync("score-view", Arg.Any<ScoreViewMap>(), new ScoreFollowOptions(true, true)));
    }
}
