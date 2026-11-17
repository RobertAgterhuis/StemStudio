using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T040/T041: toolbar toggle, session life cycle and texts.</summary>
public sealed class RecordingSessionTests : RecordingTestContext
{
    [Fact]
    public void Toggle_OpensAndClosesThePanel_WithAriaExpanded()
    {
        var toggle = Render<RecordingToolbarToggle>(p => p.Add(c => c.Session, Session));
        var button = toggle.Find("[data-testid=mixer-record-panel-toggle]");
        (button.GetAttribute("aria-expanded"), button.GetAttribute("aria-controls")).ShouldBe(("false", "rec-panel"));

        button.Click();

        Session.PanelOpen.ShouldBeTrue();
        toggle.Find("[data-testid=mixer-record-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
        Host.Refreshes.ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task ClosingWhileRecording_StopsFirst()
    {
        await OpenWithInputAsync();
        Callbacks.RaiseStateChanged("recording");

        await Session.TogglePanelAsync();

        await RecordingEngine.Received(1).StopRecordingAsync();
    }

    [Fact]
    public async Task Dispose_UnsubscribesAndReleasesTheInput()
    {
        await OpenWithInputAsync();

        await Session.DisposeAsync();
        Callbacks.RaiseStateChanged("recording");

        Session.State.ShouldBe(RecordingState.Closed);
        await RecordingEngine.Received(1).CloseInputAsync();
    }

    [Fact]
    public async Task Dispose_WhileRecording_LeavesTheInputToTheEngine()
    {
        await OpenWithInputAsync();
        Callbacks.RaiseStateChanged("recording");

        await Session.DisposeAsync();

        await RecordingEngine.DidNotReceive().CloseInputAsync();
    }

    [Fact]
    public async Task CloseInputErrors_AreSwallowed()
    {
        RecordingEngine.CloseInputAsync().ThrowsAsync(new RecordingException("Stop the recording first"));
        await OpenWithInputAsync();

        await Session.Input.CloseAsync();

        Session.Input.IsOpen.ShouldBeFalse();
    }

    [Fact]
    public async Task Record_WhileBlockedOrStarting_DoesNothing()
    {
        await Session.ToggleRecordAsync();

        await RecordingEngine.DidNotReceive().StartRecordingAsync(Arg.Any<RecordOptions>());
    }

    [Fact]
    public async Task DevicesChanged_ReListsTheInputs()
    {
        await OpenWithInputAsync();
        RecordingEngine.ListInputDevicesAsync().Returns([new InputDevice("d3", "New Mic", "g3")]);

        Callbacks.RaiseInputDevicesChanged();

        Session.Input.Devices.ShouldHaveSingleItem().Label.ShouldBe("New Mic");
    }

    [Fact]
    public async Task FinishedSession_ClearsProgress_AndFinishingIsAnnounced()
    {
        await OpenWithInputAsync();
        Callbacks.RaiseProgress(new RecordingProgress(3, null, null));
        Callbacks.RaiseStateChanged("finishing");
        Session.StatusText.ShouldBe("Finishing the take");

        Callbacks.RaiseStateChanged("open");

        (Session.Progress, Session.StatusText).ShouldBe((null, null));
    }

    [Fact]
    public async Task FailingTakeList_IsReported_AndCalibrationsFallBackToTheEstimate()
    {
        Api.ListTakesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(System.Net.HttpStatusCode.ServiceUnavailable, "Down"));
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(System.Net.HttpStatusCode.ServiceUnavailable, "Down"));

        await OpenWithInputAsync();

        Session.Takes.Error.ShouldNotBeNull();
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Estimated);
    }

    [Fact]
    public async Task Calibrations_SaveAndDelete_UpdateTheLatencyInUse()
    {
        Api.UpsertCalibrationAsync(Arg.Any<InputCalibrationInput>(), Arg.Any<CancellationToken>()).Returns(RecordingData.Calibration());
        await OpenWithInputAsync();

        var saved = await Session.Latency.SaveAsync(new InputCalibrationInput
        {
            DeviceLabel = "USB Mic",
            LatencySamples = 1102,
            Method = CalibrationMethodDto.Loopback,
            OutputLatencySamples = 300,
        });
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Loopback);
        await Session.Latency.DeleteAsync(saved);

        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Estimated);
        await Api.Received(1).DeleteCalibrationAsync(RecordingData.CalibrationId, Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0, "0:00.0")]
    [InlineData(9.96, "0:09.9")]
    [InlineData(61.55, "1:01.5")]
    [InlineData(-1, "0:00.0")]
    [InlineData(1800, "30:00.0")]
    public void Elapsed_IsMinutesSecondsTenths(double seconds, string text) => RecordingText.Elapsed(seconds).ShouldBe(text);

    [Fact]
    public void Texts_CoverEveryPartAndMilliseconds()
    {
        RecordingText.PartLabel(RecordingPartDto.Piano).ShouldBe("Keys / piano");
        RecordingText.Milliseconds(1102).ShouldBe("25.0 ms");
        RecordingText.Unsupported(new RecordingSupport(false, "noMediaDevices", null, MicrophoneAccess.Unknown)).Message.ShouldContain("Chrome");
        RecordingText.FromOpenError(new RecordingException("x")).ShouldBeNull();
    }

    [Fact]
    public void TakeItem_StatusFollowsTheServer()
    {
        var item = TakeItem.Local(RecordingData.Capture(), RecordingData.Upload());
        (item.Status, item.Saving, item.Id).ShouldBe(("local", true, "c1"));

        item.Apply(RecordingData.Take(status: TakeStatusDto.Failed));

        (item.Status, item.Saving, item.SessionId, item.StartSample).ShouldBe(("failed", false, RecordingData.SessionId, 441_000));
    }
}
