using Bunit;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Interop;
using StemStudio.Web.Recording;
using StemStudio.Web.Tests.Mixer;
using MixerPage = StemStudio.Web.Components.Mixer.Mixer;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T041/T043: the recording slots in the real mixer page and the <c>R</c> shortcut (FR-004, FR-021).</summary>
public sealed class MixerRecordingTests : MixerTestContext
{
    private readonly IRecordingEngine _recording = Substitute.For<IRecordingEngine>();
    private readonly RecordingCallbacks _callbacks = new();

    public MixerRecordingTests()
    {
        _recording.Callbacks.Returns(_callbacks);
        _recording.GetSupportAsync().Returns(new RecordingSupport(true, null, null, MicrophoneAccess.Granted));
        _recording.ListInputDevicesAsync().Returns([new InputDevice("d1", "USB Mic", "g1")]);
        _recording.OpenInputAsync(Arg.Any<OpenInputRequest>()).Returns(RecordingData.Input());
        Api.ListTakesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(RecordingData.List());
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([]);
        Services.AddSingleton(_recording);
        Services.AddSingleton(new RecordingPreferences(JSInterop.JSRuntime));
    }

    [Fact]
    public void TheToolbarToggle_OpensThePanelBelowTheConsolePanels()
    {
        var page = Ready();
        page.FindAll("[data-testid=rec-panel]").ShouldBeEmpty();

        page.Find("[data-testid=mixer-record-panel-toggle]").Click();

        page.WaitForAssertion(() => page.Find("[data-testid=rec-panel]").GetAttribute("data-permission").ShouldBe("granted"));
        page.Find("[data-testid=mixer-record-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public async Task R_OpensThePanelFirst_ThenRecords_ThenStops()
    {
        var page = Ready();

        await Command(page, "mixer.record.toggle");
        page.WaitForAssertion(() => page.Find("[data-testid=rec-panel]").ShouldNotBeNull());
        await Command(page, "mixer.record.toggle");
        await _recording.Received(1).StartRecordingAsync(Arg.Any<RecordOptions>());

        _callbacks.RaiseStateChanged("recording");
        await Command(page, "mixer.record.toggle");

        await _recording.Received(1).StopRecordingAsync();
    }

    [Fact]
    public async Task C_IsRegistered()
    {
        var page = Ready();

        await Command(page, "mixer.compare.cycle");

        page.Find("[data-testid=mixer-record-panel-toggle]").ShouldNotBeNull();
    }

    [Fact]
    public void SetToNormalSpeed_ResetsThePracticeTempo()
    {
        var page = Ready();
        page.Find("[data-testid=practice-panel-toggle]").Click();
        page.Find("[data-testid=practice-speed-down]").Click();
        page.Find("[data-testid=mixer-record-panel-toggle]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=rec-record-disabled-reason]").GetAttribute("data-reason").ShouldBe("speed"));

        page.Find("[data-testid=rec-set-normal-speed]").Click();

        page.WaitForAssertion(() => page.FindAll("[data-testid=rec-record-disabled-reason]").ShouldBeEmpty());
    }

    [Fact]
    public async Task ACapture_AppearsAsTheTakeStripAfterTheStems()
    {
        var page = Ready();
        page.Find("[data-testid=mixer-record-panel-toggle]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=rec-panel]"));
        _recording.CaptureWavAsync(Arg.Any<string>()).Returns(Substitute.For<Microsoft.JSInterop.IJSStreamReference>());
        Api.CreateTakeAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<RecordingTakeUploadDto>(), Arg.Any<CancellationToken>())
            .Returns(RecordingData.Take(status: TakeStatusDto.Saving));

        await page.InvokeAsync(() => _callbacks.RaiseTakesCaptured([RecordingData.Capture("c1")]));

        page.WaitForAssertion(() => page.Find("[data-testid=strip-take]").ShouldNotBeNull());
        page.Find(".ss-console-strips").LastElementChild!.GetAttribute("data-testid").ShouldBe("strip-take");
    }

    [Fact]
    public void WithoutTheRecordingServices_TheSlotsRenderNothing()
    {
        var bare = new BareMixer();

        bare.Ready().FindAll("[data-testid=mixer-record-panel-toggle]").ShouldBeEmpty();
    }

    [Fact]
    public async Task LeavingTheMixer_ReleasesTheMicrophone()
    {
        var page = Ready();
        page.Find("[data-testid=mixer-record-panel-toggle]").Click();
        page.WaitForAssertion(() => page.Find("[data-testid=rec-panel]"));

        await DisposeComponentsAsync();

        await _recording.Received(1).CloseInputAsync();
    }

    private IRenderedComponent<MixerPage> Ready()
    {
        var page = RenderMixer();
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Mixer).ShouldBeTrue());
        page.WaitForAssertion(() => page.FindAll("[data-testid=mixer-loading]").ShouldBeEmpty());
        return page;
    }

    private Task Command(IRenderedComponent<MixerPage> page, string command) =>
        page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Mixer, command));

    private sealed class BareMixer : MixerTestContext
    {
        public IRenderedComponent<MixerPage> Ready()
        {
            var page = RenderMixer();
            page.WaitForAssertion(() => page.FindAll("[data-testid=mixer-loading]").ShouldBeEmpty());
            return page;
        }
    }
}
