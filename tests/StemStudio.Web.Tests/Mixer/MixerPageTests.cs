using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Audio;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

public sealed class MixerPageTests : MixerTestContext
{
    [Fact]
    public void Strips_OnePerStem_InSixStemOrder_WithAllControls()
    {
        var page = RenderMixer();

        page.FindAll("[data-testid^=strip-]").Select(e => e.GetAttribute("data-testid"))
            .ShouldBe(StemNames.SixStem.Select(s => $"strip-{s}"));
        foreach (var stem in StemNames.SixStem)
        {
            page.Find($"[data-testid=fader-{stem}]").GetAttribute("aria-valuenow").ShouldBe("80");
            page.Find($"[data-testid=pan-{stem}]").GetAttribute("aria-valuenow").ShouldBe("0");
            page.Find($"[data-testid=mute-{stem}]").GetAttribute("aria-pressed").ShouldBe("false");
            page.Find($"[data-testid=solo-{stem}]").GetAttribute("aria-pressed").ShouldBe("false");
            page.Find($"[data-testid=meter-{stem}]").Id.ShouldBe($"meter-{stem}");
        }

        page.Find("[data-testid=master-fader]").GetAttribute("aria-valuenow").ShouldBe("100");
        page.Find("[data-testid=meter-master]").Id.ShouldBe("meter-master");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
        page.Find("h1").TextContent.ShouldBe("Never Gonna Give You Up");
    }

    [Fact]
    public void TheConsole_NamesItsTrack_ForSetlistPlayMode()
    {
        // ui-test-ids 1.13.0 mixer-console: E2E setlist play asserts the shown mixer's track, not the network (cached stems).
        var page = RenderMixer();

        page.Find("[data-testid=mixer-console]").GetAttribute("data-track-id").ShouldBe(TestData.TrackId.ToString("D"));
    }

    [Fact]
    public async Task Engine_IsLoaded_MetersAttached_AndStatePushed()
    {
        RenderMixer();

        await Engine.Received(1).InitializeAsync();
        await Engine.Received(1).LoadTrackAsync(Arg.Is<TrackDto>(t => t.Id == TestData.TrackId));
        foreach (var target in StemNames.SixStem.Append("master"))
        {
            await Engine.Received(1).AttachMeterAsync(target, $"meter-{target}");
        }

        await Engine.Received(6).SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Volume == 0.8 && c.Pan == 0));
        await Engine.Received(1).SetMasterAsync(1.0);
        await Engine.Received(1).SetMetronomeAsync(new MetronomeSettingsDto(true, 0.7, null, 0, 4));
        await Engine.DidNotReceiveWithAnyArgs().SetLoopAsync(default!);
    }

    [Fact]
    public async Task Fader_CallsSetChannelImmediately_AndMarksDirty()
    {
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        SetValue(page.Find("[data-testid=fader-vocals]"), 50);

        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Vocals, 0.5, 0, false, false));
        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("50");
        page.Find("[data-testid=unsaved-indicator]").TextContent.ShouldBe("Unsaved changes");
    }

    [Fact]
    public async Task Pan_Mute_Solo_CallSetChannel()
    {
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        SetValue(page.Find("[data-testid=pan-drums]"), -30);
        await page.Find("[data-testid=mute-drums]").ClickAsync(new());
        await page.Find("[data-testid=solo-bass]").ClickAsync(new());

        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Drums, 0.8, -0.3, false, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Drums, 0.8, -0.3, true, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Bass, 0.8, 0, false, true));
        page.Find("[data-testid=pan-drums]").GetAttribute("aria-valuenow").ShouldBe("-30");
        page.Find("[data-testid=mute-drums]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=solo-bass]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=strip-vocals]").ClassList.ShouldContain("ss-strip-silent");
        page.Find("[data-testid=strip-bass]").ClassList.ShouldNotContain("ss-strip-silent");
    }

    [Fact]
    public async Task MasterFader_CallsSetMaster()
    {
        var page = RenderMixer();

        SetValue(page.Find("[data-testid=master-fader]"), 60);

        await Engine.Received(1).SetMasterAsync(0.6);
        page.Find("[data-testid=master-fader]").GetAttribute("aria-valuenow").ShouldBe("60");
    }

    [Fact]
    public async Task InvalidNumber_IsIgnored()
    {
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        page.Find("[data-testid=fader-vocals]").Input("abc");

        await Engine.DidNotReceiveWithAnyArgs().SetChannelAsync(default!);
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Transport_PlayPauseStopSeek()
    {
        var page = RenderMixer();

        await page.Find("[data-testid=transport-play]").ClickAsync(new());
        await page.Find("[data-testid=transport-pause]").ClickAsync(new());
        await page.Find("[data-testid=transport-stop]").ClickAsync(new());
        page.Find("[data-testid=transport-seek]").Change("30");

        Received.InOrder(() =>
        {
            Engine.PlayAsync();
            Engine.PauseAsync();
            Engine.StopAsync();
            Engine.SeekAsync(30);
        });
        page.Find("[data-testid=transport-seek]").GetAttribute("max").ShouldBe("213.04");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public void EngineCallbacks_UpdatePositionAndState()
    {
        var page = RenderMixer();

        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 12.34);
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);

        page.WaitForAssertion(() => page.Find("[data-testid=transport-position]").TextContent.ShouldBe("0:12.3 / 3:33"));
        page.Find("[data-testid=transport-state]").TextContent.ShouldBe("Playing");
        page.Find("[data-testid=transport-play]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=transport-seek]").GetAttribute("aria-valuenow").ShouldBe("12.3");
    }

    [Fact]
    public async Task Loop_SetMarkers_Enable_AndDisable()
    {
        var page = RenderMixer();

        page.Find("[data-testid=loop-start]").Change("10");
        page.Find("[data-testid=loop-end]").Change("20");
        await page.Find("[data-testid=loop-toggle]").ClickAsync(new());

        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(10, 20, false));
        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(10, 20, true));
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
        page.FindAll("[data-testid=loop-error]").ShouldBeEmpty();

        await page.Find("[data-testid=loop-toggle]").ClickAsync(new());

        await Engine.Received(2).SetLoopAsync(new LoopRegionDto(10, 20, false));
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public async Task Loop_SetFromPosition()
    {
        var page = RenderMixer();
        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 5.0);
        await page.Find("[data-testid=loop-set-a]").ClickAsync(new());
        Engine.PositionChanged += Raise.Event<EventHandler<double>>(Engine, 9.5);
        await page.Find("[data-testid=loop-set-b]").ClickAsync(new());

        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(5, 9.5, false));
        page.Find("[data-testid=loop-start]").GetAttribute("value").ShouldBe("5");
        page.Find("[data-testid=loop-end]").GetAttribute("value").ShouldBe("9.5");
    }

    [Fact]
    public async Task Loop_InvalidMarkers_AreRefusedWithMessage()
    {
        var page = RenderMixer();

        page.Find("[data-testid=loop-start]").Change("20");
        page.Find("[data-testid=loop-end]").Change("10");
        await page.Find("[data-testid=loop-toggle]").ClickAsync(new());

        page.Find("[data-testid=loop-error]").TextContent.ShouldBe(LoopValidator.EndBeforeStartMessage);
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
        await Engine.DidNotReceiveWithAnyArgs().SetLoopAsync(default!);

        page.Find("[data-testid=loop-end]").Change("500");
        page.Find("[data-testid=loop-error]").TextContent.ShouldContain("outside the track");
    }

    [Fact]
    public async Task Loop_EngineRejection_IsShown()
    {
        Engine.SetLoopAsync(Arg.Any<LoopRegionDto>()).ThrowsAsync(new AudioEngineException("Invalid loop region: end > duration"));
        var page = RenderMixer();

        page.Find("[data-testid=loop-start]").Change("1");
        page.Find("[data-testid=loop-end]").Change("2");
        await page.Find("[data-testid=loop-toggle]").ClickAsync(new());

        page.Find("[data-testid=loop-error]").TextContent.ShouldBe("Invalid loop region: end > duration");
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("false");
    }

    [Fact]
    public void Loop_EmptyInput_IsIgnored()
    {
        var page = RenderMixer();

        page.Find("[data-testid=loop-start]").Change("");

        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
    }

    [Fact]
    public async Task EngineLoadFailure_IsShown_AndControlsDoNotCallTheEngine()
    {
        Engine.LoadTrackAsync(Arg.Any<TrackDto>()).ThrowsAsync(new AudioEngineException("Failed to load stem vocals: HTTP 404"));
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        SetValue(page.Find("[data-testid=fader-vocals]"), 10);
        await page.Find("[data-testid=transport-play]").ClickAsync(new());

        page.Find("[data-testid=mixer-engine-error]").TextContent.ShouldBe("Failed to load stem vocals: HTTP 404");
        Engine.ReceivedCalls().ShouldBeEmpty();
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task EngineCallFailure_IsShown()
    {
        Engine.PlayAsync().ThrowsAsync(new AudioEngineException("AudioContext was not allowed to start"));
        var page = RenderMixer();

        await page.Find("[data-testid=transport-play]").ClickAsync(new());

        page.Find("[data-testid=mixer-engine-error]").TextContent.ShouldBe("AudioContext was not allowed to start");
    }

    [Fact]
    public void TrackNotReady_ShowsMessage_AndDoesNotStartEngine()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track(TrackStatusDto.Separating));

        var page = RenderMixer();

        page.Find("[data-testid=mixer-load-error]").TextContent.ShouldContain("not ready");
        Engine.ReceivedCalls().ShouldBeEmpty();
    }

    [Fact]
    public void TrackNotFound_ShowsMessage()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Not found", "Track does not exist."));

        var page = RenderMixer();

        page.Find("[data-testid=mixer-load-error]").TextContent.ShouldBe("Track does not exist.");
    }

    [Fact]
    public void LeavingClean_DoesNotAsk()
    {
        RenderMixer();
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("tracks");

        navigation.Uri.ShouldBe("http://localhost/tracks");
        Confirm.ReceivedCalls().ShouldBeEmpty();
    }

    [Theory]
    [InlineData(false, "http://localhost/")]
    [InlineData(true, "http://localhost/tracks")]
    public void LeavingDirty_AsksForConfirmation(bool confirmed, string expectedUri)
    {
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(confirmed);
        var page = RenderMixer();
        SetValue(page.Find("[data-testid=fader-vocals]"), 10);
        var navigation = Services.GetRequiredService<NavigationManager>();

        navigation.NavigateTo("tracks");

        page.WaitForAssertion(() => navigation.Uri.ShouldBe(expectedUri));
        Confirm.Received(1).ConfirmAsync("Unsaved changes", Arg.Is<string>(m => m.Contains("discard")), "Leave");
    }

    [Fact]
    public async Task Dispose_ReleasesEngine_AndUnsubscribes()
    {
        var page = RenderMixer();

        await page.Instance.DisposeAsync();

        await Engine.Received(1).DisposeAsync();
        Engine.Received().PositionChanged -= Arg.Any<EventHandler<double>>();
        Engine.Received().PlaybackStateChanged -= Arg.Any<EventHandler<PlaybackState>>();
    }
}
