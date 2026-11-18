using Bunit;
using Microsoft.JSInterop;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T040-T043: input meter interop, selector edge cases, strip controls and the calibrate entry point.</summary>
public sealed class RecordingComponentTests : RecordingTestContext
{
    [Fact]
    public async Task InputMeter_WatchesTheLevelAtTwoHertz_AndStopsOnDispose()
    {
        var module = JSInterop.SetupModule("./Components/Recording/InputMeter.razor.js");
        var watch = module.SetupModule("watchMeter", _ => true);
        watch.SetupVoid("dispose").SetVoidResult();

        var meter = Render<InputMeter>();
        await meter.Instance.DisposeAsync();

        module.VerifyInvoke("watchMeter").Arguments.ShouldBe(["meter-input", 500]);
        watch.VerifyInvoke("dispose");
    }

    [Fact]
    public async Task InputMeter_DisposeAfterTheCircuitIsGone_DoesNotThrow()
    {
        var module = JSInterop.SetupModule("./Components/Recording/InputMeter.razor.js");
        var watch = module.SetupModule("watchMeter", _ => true);
        watch.SetupVoid("dispose").SetException(new JSDisconnectedException("gone"));
        var meter = Render<InputMeter>();

        await meter.Instance.DisposeAsync();

        meter.Find("[data-testid=meter-input]").ShouldNotBeNull();
    }

    [Fact]
    public void InputMeter_HasADefiniteHeight_SoTheRecordingPanelGrowsWithItsContent()
    {
        // The strip meter wrap is height: 100 %; inside the wrapping recording panel (a grid row of the console) that
        // percentage made the meter as tall as the panel and pushed Record under the channel strips (r14 E2E).
        var module = JSInterop.SetupModule("./Components/Recording/InputMeter.razor.js");
        module.SetupModule("watchMeter", _ => true);

        var meter = Render<InputMeter>();

        meter.Find(".ss-meter-wrap").ClassList.ShouldContain("ss-input-meter");
        var rule = CssRule(Fakes.RepoPaths.ReadWeb("wwwroot/css/components/mixer.css"), ".ss-meter-wrap.ss-input-meter {");
        rule.ShouldContain("flex: 0 0 100%;");
        var height = System.Text.RegularExpressions.Regex.Match(rule, @"(?m)^\s*height:\s*([^;]+);").Groups[1].Value;
        height.ShouldNotBeEmpty();
        height.ShouldNotContain("%");
    }

    private static string CssRule(string css, string selector)
    {
        var start = css.IndexOf(selector, StringComparison.Ordinal);
        start.ShouldBeGreaterThanOrEqualTo(0, selector);
        return css[start..css.IndexOf('}', start)];
    }

    [Fact]
    public async Task InputSelector_IgnoresUnknownValues()
    {
        await OpenWithInputAsync();
        RecordingEngine.ClearReceivedCalls();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-input-mode]").Change("surround");
        panel.Find("[data-testid=rec-part-select]").Change("kazoo");
        panel.Find("[data-testid=rec-count-in]").Change("5");
        panel.Find("[data-testid=rec-monitoring-volume]").Change("loud");

        await RecordingEngine.DidNotReceive().OpenInputAsync(Arg.Any<OpenInputRequest>());
        Session.Input.Settings.Part.ShouldBe(RecordingPartDto.Vocals);
        Session.Input.Settings.CountInBars.ShouldBe(1);
    }

    [Fact]
    public async Task Calibrate_RaisesTheDialogRequest()
    {
        var asked = 0;
        await OpenWithInputAsync();
        var panel = RenderAtLevel<RecordingPanel>(ExperienceLevel.Beginner, p => p
            .Add(c => c.Session, Session)
            .Add(c => c.OnCalibrate, () => asked++));

        panel.Find("[data-testid=rec-calibrate]").Click();

        asked.ShouldBe(1);
    }

    [Fact]
    public async Task TakeStrip_PanAndSolo_ReachTheEngine_AndNotifyTheMixer()
    {
        var changed = 0;
        await LoadACaptureAsync();
        var strip = RenderAtLevel<TakeStrip>(ExperienceLevel.Advanced, p => p
            .Add(c => c.Takes, Session.Takes)
            .Add(c => c.OnChanged, () => changed++));

        strip.Find("[data-testid=pan-take]").Input("-50");
        strip.Find("[data-testid=solo-take]").Click();

        await Engine.Received().SetChannelAsync(new ChannelSettingsDto("take", 0.8, -0.5, false, true));
        changed.ShouldBe(2);
    }

    [Fact]
    public async Task TakeStrip_RebindsTheMeterOnlyForANewTake()
    {
        await LoadACaptureAsync();
        var strip = Render<TakeStrip>(p => p.Add(c => c.Takes, Session.Takes));

        strip.Render();

        await Engine.Received(1).AttachMeterAsync("take", "meter-take");
    }

    [Fact]
    public async Task DeviceListFailure_AfterADeviceChange_IsShownAsError()
    {
        await OpenWithInputAsync();
        RecordingEngine.ListInputDevicesAsync().ThrowsAsync(RecordingException.FromEngine("enumerate failed", new InvalidOperationException()));

        Callbacks.RaiseInputDevicesChanged();

        Session.Error.ShouldBe("enumerate failed");
        Session.Latency.Calibrations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ASecondSessionWhileSaving_IsPolledByTheRunningLoop()
    {
        var wav = Wav();
        RecordingEngine.CaptureWavAsync(Arg.Any<string>()).Returns(wav);
        Api.CreateTakeAsync(TrackId, Arg.Any<Stream>(), Arg.Any<RecordingTakeUploadDto>(), Arg.Any<CancellationToken>())
            .Returns(Take(status: TakeStatusDto.Saving));
        Api.GetTakeAsync(TakeId, Arg.Any<CancellationToken>()).Returns(Take());
        await OpenWithInputAsync();
        Callbacks.RaiseTakesCaptured([Capture("c1")]);
        var waits = Timer.Waits;

        Callbacks.RaiseTakesCaptured([Capture("c2")]);

        Timer.Waits.ShouldBe(waits);
        await TickAsync();
        Session.Takes.Items.ShouldAllBe(i => i.Status == "ready");
    }

    private async Task LoadACaptureAsync()
    {
        await OpenWithInputAsync();
        await Session.Takes.AddAsync([TakeItem.Local(Capture("c1"), Upload())]);
    }
}
