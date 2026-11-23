using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.InstrumentViews;

/// <summary>Singing feedback in the mixer (T105, FR-042, FR-044): the lead vocal is muted while it runs, the banner shows the score.</summary>
public sealed class MixerSingingTests : MixerTestContext
{
    public MixerSingingTests()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(DrumlessTests.DrumTrack());
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        Api.GetVocalPitchAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(InstrumentViewsData.VocalPitch());
        Views.StopSingingFeedbackAsync().Returns(new SingingSummary());
    }

    private IRenderedComponent<MixerPage> OpenSinging()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").HasAttribute("disabled").ShouldBeFalse());
        page.InvokeAsync(() => page.Instance.OpenView(InstrumentViewDto.Singing));
        return page;
    }

    [Fact]
    public void TheView_HostsTheSingingPanel_OutsideTheDock()
    {
        var page = OpenSinging();

        var view = page.Find("[data-testid=instrument-view]");
        (view.GetAttribute("data-view"), view.GetAttribute("aria-label")).ShouldBe(("singing", "Singing feedback"));
        view.QuerySelector("[data-testid=singing]").ShouldNotBeNull();
        page.Find(".ss-mixer-dock").QuerySelector("[data-testid=singing]").ShouldBeNull();
    }

    [Fact]
    public async Task TheEndOfTheSong_EndsTheFeedback_AndShowsTheSummary()
    {
        var page = OpenSinging();
        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());

        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Stopped);

        page.WaitForAssertion(() => page.FindAll("[data-testid=singing-summary]").ShouldNotBeEmpty());
        await Views.Received(1).StopSingingFeedbackAsync();
    }

    [Fact]
    public async Task WhileRunning_TheLeadVocalIsMuted_AndRestoredAfterwards()
    {
        var page = OpenSinging();
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());

        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals.lead" && c.Mute));
        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals.lead" && !c.Mute));
    }

    [Fact]
    public async Task WithTheVocalsCollapsed_TheGroupIsExpandedWhileRunning_AndCollapsedAfterwards()
    {
        var page = OpenSinging();
        page.Find("[data-testid=expand-vocals]").GetAttribute("aria-expanded").ShouldBe("false");
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());

        await Engine.Received(1).SetExpandedAsync("vocals", true);
        page.Find("[data-testid=expand-vocals]").GetAttribute("aria-expanded").ShouldBe("true");
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals.lead" && c.Mute));
        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());
        await Engine.Received(1).SetExpandedAsync("vocals", false);
        page.Find("[data-testid=expand-vocals]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task WhenTheEngineCannotExpand_TheParentVocalsAreMuted()
    {
        var page = OpenSinging();
        Engine.SetExpandedAsync("vocals", true).Returns(Task.FromException(new AudioEngineException("Failed to load stem")));
        Engine.ClearReceivedCalls();

        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());

        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals" && c.Mute));
        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals" && !c.Mute));
        await Engine.DidNotReceive().SetExpandedAsync("vocals", false);
    }

    [Fact]
    public async Task TheLastUnitScore_StandsNextToTheBanner()
    {
        var page = OpenSinging();
        await page.Find("[data-testid=singing-toggle]").ClickAsync(new MouseEventArgs());

        Views.SingingUnitScored += Raise.Event<EventHandler<SingingUnitScore>>(Views, new SingingUnitScore(0, 92, 1));

        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-score]").GetAttribute("data-score").ShouldBe("92"));
        var score = page.Find("[data-testid=karaoke-score]");
        (score.GetAttribute("data-unit-index"), score.TextContent.Trim()).ShouldBe(("0", "Line 1: 92"));
        score.Closest(".ss-mixer-dock").ShouldNotBeNull();
        Views.SingingUnitScored += Raise.Event<EventHandler<SingingUnitScore>>(Views, new SingingUnitScore(1, null, 1));
        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-score]").GetAttribute("data-score").ShouldBe(string.Empty));
    }

    [Fact]
    public void BeforeAnyScore_TheBannerHasNoScore()
    {
        var page = OpenSinging();

        page.FindAll("[data-testid=karaoke-score]").ShouldBeEmpty();
    }
}
