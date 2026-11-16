using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Agent;
using StemStudio.Web.Audio;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

/// <summary>The open mixer page registers the mixer UI tools for its track (T039, DELTA.md section 4).</summary>
public sealed class MixerAgentToolsTests : MixerTestContext
{
    private AgentUiToolHost Host => Services.GetRequiredService<AgentUiToolHost>();

    private static UiRequestedEventDto Request(string tool, string arguments) => new()
    {
        StepId = AgentData.StepId,
        Tool = tool,
        Arguments = AgentData.Json(arguments),
        TimeoutSeconds = AgentContractLimits.UiToolTimeoutSeconds,
    };

    [Fact]
    public async Task AgentChannelChange_GoesThroughThePageHandler_ToTheEngineAndTheFader()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Host.HasMixer.ShouldBeTrue());

        var result = await page.InvokeAsync(() => Host.ExecuteAsync(Request("mixer_set_channel", """{"stem":"vocals","gainDb":-6,"mute":true}""")));

        result.Ok.ShouldBeTrue();
        await Engine.Received().SetChannelAsync(Arg.Is<ChannelSettingsDto>(c => c.Stem == "vocals" && c.Mute));
        page.WaitForAssertion(() => page.Find("[data-testid=mute-vocals]").GetAttribute("aria-pressed").ShouldBe("true"));
        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("50");
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task AgentTransportAndLoop_UseTheEngine()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Host.HasMixer.ShouldBeTrue());

        await page.InvokeAsync(() => Host.ExecuteAsync(Request("transport", """{"action":"seek","positionSeconds":30}""")));
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("transport", """{"action":"play"}""")));
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("transport", """{"action":"pause"}""")));
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("transport", """{"action":"stop"}""")));
        var loop = await page.InvokeAsync(() => Host.ExecuteAsync(Request("set_loop", """{"startSeconds":10,"endSeconds":20,"enabled":true}""")));
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("mixer_set_master", """{"gainDb":-6}""")));
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("set_metronome", """{"enabled":false}""")));

        loop.Ok.ShouldBeTrue();
        await Engine.Received(1).SeekAsync(30);
        await Engine.Received(1).PlayAsync();
        await Engine.Received(1).PauseAsync();
        await Engine.Received(1).StopAsync();
        await Engine.Received().SetLoopAsync(new LoopRegionDto(10, 20, true));
        await Engine.Received(1).SetMasterAsync(Arg.Is<double>(v => Math.Abs(v - 0.501) < 0.001));
        await Engine.Received().SetMetronomeAsync(Arg.Is<MetronomeSettingsDto>(m => !m.Enabled));
    }

    [Fact]
    public async Task GetState_ReportsTheLoadedMix_AndThePlaybackState()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        var page = RenderMixer(TestData.MixId);
        page.WaitForAssertion(() => Host.HasMixer.ShouldBeTrue());
        Engine.PlaybackStateChanged += Raise.Event<EventHandler<PlaybackState>>(Engine, PlaybackState.Playing);

        var result = await page.InvokeAsync(() => Host.ExecuteAsync(Request("mixer_get_state", "{}")));

        result.Result!.Value.GetProperty("playing").GetBoolean().ShouldBeTrue();
        page.WaitForAssertion(() => Host.OpenMixId(TestData.TrackId).ShouldBe(TestData.MixId));
        Host.OpenMixId(Guid.NewGuid()).ShouldBeNull();
    }

    [Fact]
    public async Task CommandFromTheMixer_CarriesTheLoadedMixId_EvenWithoutItInTheAddress()
    {
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        var page = RenderMixer(TestData.MixId);
        page.WaitForAssertion(() => Host.OpenMixId(TestData.TrackId).ShouldBe(TestData.MixId));
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().NavigateTo($"tracks/{TestData.TrackId}/mixer");
        var panel = Services.GetRequiredService<AgentPanelState>();
        await panel.LoadAsync();

        (await panel.SendAsync("Zet de zang zachter")).ShouldBeTrue();

        await AgentApi.Received(1).SendMessageAsync(Arg.Any<Guid>(),
            Arg.Is<SendAgentMessageRequest>(r => r.Context.Page == "mixer" && r.Context.TrackId == TestData.TrackId && r.Context.MixId == TestData.MixId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ShowKaraoke_TurnsTheHiddenBannerOn_ThroughTheKaraokeToggle()
    {
        JSInterop.Setup<string?>("localStorage.getItem", "stemstudio.karaoke").SetResult("{\"visible\":false,\"size\":\"m\"}");
        Api.GetLyricsAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(LyricsData.Found());
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-toggle]").GetAttribute("aria-pressed").ShouldBe("false"));

        var result = await page.InvokeAsync(() => Host.ExecuteAsync(Request("show_karaoke", """{"enabled":true}""")));

        (result.Ok, result.Result!.Value.GetProperty("previous").GetBoolean(), result.Result!.Value.GetProperty("current").GetBoolean()).ShouldBe((true, false, true));
        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-toggle]").GetAttribute("aria-pressed").ShouldBe("true"));
        page.WaitForAssertion(() => Engine.Received(1).AttachKaraokeAsync("karaoke-host", Arg.Any<KaraokeLyrics>(), Arg.Any<KaraokeOptions>()));
    }

    [Fact]
    public async Task ShowKaraoke_WithoutSyncedLyrics_FailsReadably()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => page.Find("[data-testid=karaoke-unavailable]").ShouldNotBeNull());

        var result = await page.InvokeAsync(() => Host.ExecuteAsync(Request("show_karaoke", """{"enabled":true}""")));

        (result.Ok, result.Error).ShouldBe((false, "This track has no synced lyrics, so the karaoke banner cannot be shown."));
    }

    [Theory]
    [InlineData("save_mix_as")]
    [InlineData("update_mix")]
    public async Task AgentSave_ClearsTheUnsavedIndicator_AndKeepsTheSavedMixOpen(string tool)
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix("Bass practice"));
        var page = await RenderChangedMixerAsync();

        await RunSaveStepAsync(tool, AgentStepStatusDto.Succeeded, $$"""{"mixId":"{{TestData.MixId}}","name":"Bass practice"}""");

        page.WaitForAssertion(() => page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty());
        Host.OpenMixId(TestData.TrackId).ShouldBe(TestData.MixId);
        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>().Uri.ShouldEndWith($"tracks/{TestData.TrackId}/mixer?mixId={TestData.MixId}");
    }

    [Theory]
    [InlineData("save_mix_as", AgentStepStatusDto.Failed, """{"mixId":"f1b2c3d4-0000-4000-8000-0000000000ff"}""")]
    [InlineData("save_mix_as", AgentStepStatusDto.Succeeded, """{"name":"Bass practice"}""")]
    [InlineData("save_mix_as", AgentStepStatusDto.Succeeded, null)]
    [InlineData("search_library", AgentStepStatusDto.Succeeded, """{"mixId":"f1b2c3d4-0000-4000-8000-0000000000ff"}""")]
    public async Task OtherSteps_LeaveTheUnsavedIndicator(string tool, AgentStepStatusDto status, string? summary)
    {
        var page = await RenderChangedMixerAsync();

        await RunSaveStepAsync(tool, status, summary);

        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        await Api.DidNotReceive().GetMixAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AgentSave_OfAnotherTracksMix_LeavesTheUnsavedIndicator()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix() with { TrackId = Guid.NewGuid() });
        var page = await RenderChangedMixerAsync();

        await RunSaveStepAsync("save_mix_as", AgentStepStatusDto.Succeeded, $$"""{"mixId":"{{TestData.MixId}}"}""");

        await Api.Received(1).GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>());
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
        Host.OpenMixId(TestData.TrackId).ShouldBeNull();
    }

    [Fact]
    public async Task AgentSave_WhenTheMixCannotBeRead_LeavesTheUnsavedIndicator()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns<MixDto>(_ => throw new StemStudio.Web.Api.ApiException(StemStudio.Web.Api.ApiError.Unreachable));
        var page = await RenderChangedMixerAsync();

        await RunSaveStepAsync("save_mix_as", AgentStepStatusDto.Succeeded, $$"""{"mixId":"{{TestData.MixId}}"}""");

        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task AgentSave_WithoutAnOpenMixer_ChangesNothing()
    {
        await Panel.LoadAsync();
        (await Panel.SendAsync("Sla op als Bass practice")).ShouldBeTrue();

        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step(1, "save_mix_as", AgentStepStatusDto.Running, ToolRiskDto.Change))
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = AgentStepStatusDto.Succeeded, ResultSummary = AgentData.Json($$"""{"mixId":"{{TestData.MixId}}"}""") })
            .Completed()
            .End();
        await Panel.Following;

        await Api.DidNotReceive().GetMixAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UserSaveAs_WhileTheAgentPanelIsOpen_ClearsTheUnsavedIndicator()
    {
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(TestData.Mix("Bass practice"));
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix("Bass practice"));
        var page = await RenderChangedMixerAsync();

        await page.Find("[data-testid=mix-save]").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        page.WaitForElement("[data-testid=mix-name-input]").Input("Bass practice");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());

        page.WaitForAssertion(() => page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty());
    }

    private async Task<IRenderedComponent<StemStudio.Web.Components.Mixer.Mixer>> RenderChangedMixerAsync()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Host.HasMixer.ShouldBeTrue());
        await page.InvokeAsync(() => Host.ExecuteAsync(Request("mixer_set_channel", """{"stem":"drums","mute":true}""")));
        page.WaitForElement("[data-testid=unsaved-indicator]");
        Panel.Toggle();
        await Panel.LoadAsync();
        return page;
    }

    private async Task RunSaveStepAsync(string tool, AgentStepStatusDto status, string? summary)
    {
        (await Panel.SendAsync("Sla op als Bass practice")).ShouldBeTrue();
        AgentEvents.Started()
            .Push(AgentEventTypes.StepCreated, AgentData.Step(1, tool, AgentStepStatusDto.Running, ToolRiskDto.Change))
            .Push(AgentEventTypes.StepUpdated, new StepUpdatedEventDto { StepId = AgentData.StepId, Status = status, ResultSummary = summary is null ? null : AgentData.Json(summary) })
            .Completed()
            .End();
        await Panel.Following;
    }

    [Fact]
    public async Task ClosingThePage_RemovesTheTools()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Host.HasMixer.ShouldBeTrue());

        await page.Instance.DisposeAsync();

        Host.HasMixer.ShouldBeFalse();
    }
}
