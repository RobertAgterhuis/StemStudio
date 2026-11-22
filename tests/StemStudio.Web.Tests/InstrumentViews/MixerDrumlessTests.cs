using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>"Drumless" in the mixer and the drum trainer it opens (T090, FR-032, FR-033, data-model 5.3 and 8).</summary>
public sealed class MixerDrumlessTests : MixerTestContext
{
    private static readonly string StoreKey = $"stemstudio.drumless.previous.{TestData.TrackId}";

    public MixerDrumlessTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(DrumlessTests.DrumTrack());
        Api.GetDrumHitsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.DrumHits());
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([]);
    }

    private IRenderedComponent<MixerPage> Ready(IRenderedComponent<MixerPage> page)
    {
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        Engine.ClearReceivedCalls();
        return page;
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        RenderAtLevel<MixerPage>(level, p => p.Add(c => c.Id, TestData.TrackId));

    [Fact]
    public async Task Drumless_MutesEveryDrumChannel_SwitchesTheClickOn_AndOpensTheTrainer()
    {
        var page = Ready(RenderMixer());
        var button = page.Find("[data-testid=drumless]");
        button.GetAttribute("aria-pressed").ShouldBe("false");

        await button.ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=drumless]").GetAttribute("aria-pressed").ShouldBe("true");
        foreach (var stem in new[] { "drums", "drums.kick", "drums.snare", "drums.hihat", "drums.rest" })
        {
            await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == stem && c.Mute));
        }

        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "bass" && !c.Mute));
        await Engine.Received().SetMetronomeAsync(Arg.Is<MetronomeSettingsDto>(m => m.Enabled));
        page.Find("[data-testid=countin-toggle]").GetAttribute("aria-checked").ShouldBe("true");
        var view = page.Find("[data-testid=instrument-view]");
        (view.GetAttribute("data-view"), view.GetAttribute("aria-label")).ShouldBe(("drum-trainer", "Drum trainer"));
        view.QuerySelector("[data-testid=drum-trainer]").ShouldNotBeNull();
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments[0].ShouldBe(StoreKey);
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task ASecondClick_RestoresTheDrumsAndTheClick()
    {
        var page = Ready(RenderMixer());
        await page.Find("[data-testid=metronome-toggle]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());

        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=drumless]").GetAttribute("aria-pressed").ShouldBe("false");
        page.Find("[data-testid=mute-drums]").GetAttribute("aria-pressed").ShouldBe("false");
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.kick" && !c.Mute));
        await Engine.Received().SetMetronomeAsync(Arg.Is<MetronomeSettingsDto>(m => !m.Enabled));
        page.Find("[data-testid=countin-toggle]").GetAttribute("aria-checked").ShouldBe("false");
        JSInterop.VerifyInvoke("localStorage.removeItem").Arguments[0].ShouldBe(StoreKey);
    }

    [Fact]
    public async Task Advanced_TheGuidePiece_IsAudibleOnTopOfTheDrumlessMix()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=drum-lane-label-hihat]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=drum-lane-label-hihat]").GetAttribute("aria-pressed").ShouldBe("true");
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.hihat" && !c.Mute));
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.kick" && c.Mute));
        await page.Find("[data-testid=drum-lane-label-hihat]").ClickAsync(new MouseEventArgs());
        page.Find("[data-testid=drum-lane-label-hihat]").GetAttribute("aria-pressed").ShouldBe("false");
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.hihat" && c.Mute));
    }

    [Fact]
    public async Task AGuide_OnCollapsedDrums_ExpandsTheGroup_SoThePieceIsHeard_AndCollapsesWhenCleared()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("false");
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=drum-lane-label-kick]").ClickAsync(new MouseEventArgs());

        await Engine.Received(1).SetExpandedAsync("drums", true);
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("true");
        await page.Find("[data-testid=drum-lane-label-snare]").ClickAsync(new MouseEventArgs());
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.kick" && c.Mute));
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "drums.snare" && !c.Mute));
        await page.Find("[data-testid=drum-lane-label-snare]").ClickAsync(new MouseEventArgs());
        await Engine.Received(1).SetExpandedAsync("drums", false);
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task LeavingDrumless_WithAGuide_CollapsesTheGroupAgain()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-testid=drum-lane-label-hihat]").ClickAsync(new MouseEventArgs());
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());

        await Engine.Received(1).SetExpandedAsync("drums", false);
        page.Find("[data-testid=expand-drums]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task AGuide_WithoutDrumless_TurnsDrumlessOnFirst()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());

        await page.Find("[data-testid=drum-lane-label-snare]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=drumless]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=mute-drums]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=drum-lane-label-snare]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public async Task AClickWhileDrumlessIsStillBeingApplied_IsIgnored_SoTheEngineEndsConsistent()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Engine.SetChannelAsync(Arg.Any<ChannelSettingsDto>()).Returns(_ => gate.Task);
        Engine.ClearReceivedCalls();

        var restore = page.InvokeAsync(() => page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs()));
        var guide = page.InvokeAsync(() => page.Find("[data-testid=drum-lane-label-kick]").ClickAsync(new MouseEventArgs()));
        gate.SetResult();
        await restore;
        await guide;

        page.WaitForAssertion(() => page.Find("[data-testid=drumless]").GetAttribute("aria-pressed").ShouldBe("false"));
        var last = Engine.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(Engine.SetChannelAsync))
            .Select(c => (ChannelSettingsDto)c.GetArguments()[0]!).GroupBy(c => c.Stem).ToDictionary(g => g.Key, g => g.Last());
        foreach (var stem in new[] { "drums", "drums.kick", "drums.snare", "drums.hihat", "drums.rest" })
        {
            last[stem].Mute.ShouldBeFalse(stem);
        }
    }

    [Fact]
    public void AStoredRestore_WhileTheDrumsAreAudible_IsDropped()
    {
        JSInterop.Setup<string?>("localStorage.getItem", StoreKey).SetResult("""{"flags":[],"metronomeEnabled":false,"countInBars":0}""");

        var page = Ready(RenderMixer());

        page.WaitForAssertion(() => JSInterop.VerifyInvoke("localStorage.removeItem").Arguments[0].ShouldBe(StoreKey));
        page.Find("[data-testid=drumless]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void TheToggleAndTheTrainer_LiveOutsideTheDock()
    {
        var page = Ready(RenderMixer());

        page.Find("[data-testid=drumless]").Click();

        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=drumless]").ShouldBeNull();
        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=instrument-view]").ShouldBeNull();
        page.Find("[data-testid=drumless]").GetAttribute("data-help-id").ShouldBe("drumless");
    }

    [Fact]
    public void WhileTheTrainerIsOpen_TheMixerScorePanelMakesRoomForTheDrumScore()
    {
        Api.ListTrackScoresAsync(TestData.TrackId, Arg.Any<CancellationToken>())
            .Returns([PracticeFlowData.TrackScore() with { Profile = StemStudio.Contracts.Scores.ScoreProfileDto.Drums, Source = "drums" }]);
        Api.GetScoreViewAsync(PracticeFlowData.ScoreAssetId, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.ScoreView());
        var page = Ready(RenderMixer());
        page.Find("[data-testid=score-panel-toggle]").Click();
        page.WaitForAssertion(() => page.FindAll("[data-testid=score-panel]").Count.ShouldBe(1));

        page.Find("[data-testid=drumless]").Click();

        page.WaitForAssertion(() => page.FindAll("[data-testid=score-panel]").Count.ShouldBe(1));
        page.Find("[data-testid=drum-score] [data-testid=score-panel]").ShouldNotBeNull();
        page.Find("[data-testid=score-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheMetronome_IsRestoredToItsSettingBeforeDrumless(bool onBefore)
    {
        var page = Ready(RenderMixer());
        var toggle = page.Find("[data-testid=metronome-toggle]");
        if ((toggle.GetAttribute("aria-pressed") == "true") != onBefore)
        {
            await toggle.ClickAsync(new MouseEventArgs());
        }

        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());
        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
        await page.Find("[data-testid=drumless]").ClickAsync(new MouseEventArgs());

        page.Find("[data-testid=metronome-toggle]").GetAttribute("aria-pressed").ShouldBe(onBefore ? "true" : "false");
    }
}
