using System.Net;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Mixer;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer;

public sealed class SaveMixTests : MixerTestContext
{
    private static readonly Guid NewMixId = Guid.Parse("99999999-8888-7777-6666-555555555555");

    [Fact]
    public async Task FirstSave_OpensNameDialog_PostsMix_AndClearsDirty()
    {
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>())
            .Returns(c => TestData.Mix("Practice") with { Id = NewMixId, Settings = c.Arg<CreateMixRequest>().Settings });
        var page = RenderMixer();
        SetValue(page.Find("[data-testid=fader-guitar]"), 0);

        await page.Find("[data-testid=mix-save]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input("  Practice ");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        await Api.Received(1).CreateMixAsync(
            Arg.Is<CreateMixRequest>(r => r.TrackId == TestData.TrackId && r.Settings.Name == "Practice"
                && r.Settings.Channels.Count == 6 && r.Settings.Channels.Single(c => c.Stem == StemNames.Guitar).Volume == 0
                && r.Settings.MasterVolume == 1 && r.Settings.Loop == null),
            Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
        page.FindAll("[data-testid=mix-name-input]").ShouldBeEmpty();
        page.Find("[data-testid=mix-name]").TextContent.ShouldBe("Practice");
        NavigatedTo.ShouldBe($"http://localhost/tracks/{TestData.TrackId}/mixer?mixId={NewMixId}");
        await Api.DidNotReceive().GetMixAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task NameTaken_ShowsMessage_InDialog()
    {
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.Conflict, "Mix name taken", "A mix named \"Practice\" already exists for this track."));
        var page = RenderMixer();

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input("Practice");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe("The name \"Practice\" is taken for this track. Choose another name.");
        page.Find("[data-testid=mix-name-input]").ShouldNotBeNull();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("01234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890")]
    public async Task InvalidName_IsRefused_WithoutCallingApi(string name)
    {
        var page = RenderMixer();

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").Input(name);
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe(MixNames.LengthMessage);
        await Api.DidNotReceiveWithAnyArgs().CreateMixAsync(default!, Xunit.TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task Cancel_ClosesDialog()
    {
        var page = RenderMixer();

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        await page.Find("[data-testid=mix-save-cancel]").ClickAsync(new());

        page.FindAll("[data-testid=mix-name-input]").ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadingMix_RestoresEverySetting_AndPushesThemToTheEngine()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());

        var page = RenderMixer(TestData.MixId);

        page.Find("[data-testid=fader-guitar]").GetAttribute("aria-valuenow").ShouldBe("0");
        page.Find("[data-testid=mute-guitar]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=pan-drums]").GetAttribute("aria-valuenow").ShouldBe("-20");
        page.Find("[data-testid=solo-drums]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("80");
        page.Find("[data-testid=master-fader]").GetAttribute("aria-valuenow").ShouldBe("90");
        page.Find("[data-testid=metronome-bpm]").GetAttribute("value").ShouldBe("120");
        page.Find("[data-testid=metronome-offset]").GetAttribute("value").ShouldBe("25");
        page.Find("[data-testid=metronome-beats-per-bar]").GetAttribute("value").ShouldBe("3");
        page.Find("[data-testid=loop-start]").GetAttribute("value").ShouldBe("10");
        page.Find("[data-testid=loop-end]").GetAttribute("value").ShouldBe("20");
        page.Find("[data-testid=loop-toggle]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=mix-name]").TextContent.ShouldBe("Guitar practice");
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Guitar, 0, 0, true, false));
        await Engine.Received(1).SetChannelAsync(new ChannelSettingsDto(StemNames.Drums, 0.9, -0.2, false, true));
        await Engine.Received(1).SetMasterAsync(0.9);
        await Engine.Received(1).SetMetronomeAsync(new MetronomeSettingsDto(true, 0.5, 120, 25, 3));
        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(10, 20, true));
    }

    [Fact]
    public async Task SaveOnLoadedMix_PutsSettings_AndClearsDirty()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.UpdateMixAsync(TestData.MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        var page = RenderMixer(TestData.MixId);
        SetValue(page.Find("[data-testid=fader-vocals]"), 30);

        await page.Find("[data-testid=mix-save]").ClickAsync(new());

        await Api.Received(1).UpdateMixAsync(TestData.MixId,
            Arg.Is<MixSettingsDto>(s => s.Name == "Guitar practice" && s.Channels.Single(c => c.Stem == StemNames.Vocals).Volume == 0.3
                && s.Loop == new LoopRegionDto(10, 20, true) && s.Metronome.BpmOverride == 120),
            Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=unsaved-indicator]").ShouldBeEmpty();
        page.FindAll("[data-testid=mix-name-input]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SaveFailure_IsShown_AndStaysDirty()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.UpdateMixAsync(TestData.MixId, Arg.Any<MixSettingsDto>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(HttpStatusCode.NotFound, "Not found", "Mix does not exist."));
        var page = RenderMixer(TestData.MixId);
        SetValue(page.Find("[data-testid=fader-vocals]"), 30);

        await page.Find("[data-testid=mix-save]").ClickAsync(new());

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe("Mix does not exist.");
        page.Find("[data-testid=unsaved-indicator]").ShouldNotBeNull();
    }

    [Fact]
    public async Task SaveAs_OnLoadedMix_CreatesNewMix_WithSuggestedName()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        Api.CreateMixAsync(Arg.Any<CreateMixRequest>(), Arg.Any<CancellationToken>()).Returns(TestData.Mix("Guitar practice 2") with { Id = NewMixId });
        var page = RenderMixer(TestData.MixId);

        await page.Find("[data-testid=mix-save-as]").ClickAsync(new());
        page.Find("[data-testid=mix-name-input]").GetAttribute("value").ShouldBe("Guitar practice");
        page.Find("[data-testid=mix-name-input]").Input("Guitar practice 2");
        await page.Find("[data-testid=mix-save-confirm]").ClickAsync(new());

        await Api.Received(1).CreateMixAsync(Arg.Is<CreateMixRequest>(r => r.Settings.Name == "Guitar practice 2"), Arg.Any<CancellationToken>());
        await Api.DidNotReceiveWithAnyArgs().UpdateMixAsync(default, default!, Xunit.TestContext.Current.CancellationToken);
        page.Find("[data-testid=mix-name]").TextContent.ShouldBe("Guitar practice 2");
    }

    [Fact]
    public void MixLoadFailure_IsShown()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Not found", "Mix does not exist."));

        var page = RenderMixer(TestData.MixId);

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe("Mix does not exist.");
        page.Find("[data-testid=fader-vocals]").GetAttribute("aria-valuenow").ShouldBe("80");
    }

    [Fact]
    public void MixOfAnotherTrack_IsRefused()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix() with { TrackId = Guid.NewGuid() });

        var page = RenderMixer(TestData.MixId);

        page.Find("[data-testid=mix-error]").TextContent.ShouldBe(MixNames.WrongTrackMessage);
        page.FindAll("[data-testid=mix-name]").ShouldBeEmpty();
    }

    [Fact]
    public async Task SwitchingMix_AfterEngineReady_ReappliesFullMix()
    {
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
        var page = RenderMixer();
        Engine.ClearReceivedCalls();

        Services.GetRequiredService<Microsoft.AspNetCore.Components.NavigationManager>()
            .NavigateTo($"tracks/{TestData.TrackId}/mixer?mixId={TestData.MixId}");

        page.WaitForAssertion(() => page.Find("[data-testid=mix-name]").TextContent.ShouldBe("Guitar practice"));
        await Engine.Received(6).SetChannelAsync(Arg.Any<ChannelSettingsDto>());
        await Engine.Received(1).SetLoopAsync(new LoopRegionDto(10, 20, true));
        await Engine.DidNotReceive().LoadTrackAsync(Arg.Any<StemStudio.Contracts.Tracks.TrackDto>());
    }

    [Fact]
    public void MixerState_ApplyAndExport_RoundTrip()
    {
        var state = new MixerState(TestData.Track());
        state.SetVolume(StemNames.Bass, 0.1);

        state.ApplyMix(TestData.Mix());

        state.IsDirty.ShouldBeFalse();
        state.MixId.ShouldBe(TestData.MixId);
        state.Channel(StemNames.Bass).Volume.ShouldBe(0.8);
        var exported = state.ToSettings("Guitar practice");
        exported.MasterVolume.ShouldBe(0.9);
        exported.Metronome.ShouldBe(TestData.Settings().Metronome);
        exported.Loop.ShouldBe(TestData.Settings().Loop);
        exported.Channels.Single(c => c.Stem == StemNames.Guitar).ShouldBe(TestData.Settings().Channels[0]);
    }

    [Fact]
    public void MixNames_Rules()
    {
        MixNames.Validate(" a ").ShouldBeNull();
        MixNames.Validate(new string('x', 100)).ShouldBeNull();
        MixNames.Validate("").ShouldBe(MixNames.LengthMessage);
        MixNames.Validate(null).ShouldBe(MixNames.LengthMessage);
    }
}
