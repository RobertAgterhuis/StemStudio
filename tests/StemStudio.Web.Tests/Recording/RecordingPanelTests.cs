using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T040: recording panel, permission gate and input selector (FR-001..FR-006, FR-009, FR-020, FR-021).</summary>
public sealed class RecordingPanelTests : RecordingTestContext
{
    [Fact]
    public async Task BeforePermission_ShowsThePrivacyNoteAndAllow_AndOpensNothing()
    {
        await Session.TogglePanelAsync();

        var panel = RenderPanel();

        panel.Find("[data-testid=rec-privacy-note]").TextContent.ShouldContain("Recordings stay on this computer");
        panel.Find("[data-testid=rec-panel]").GetAttribute("data-permission").ShouldBe("prompt");
        panel.Find("[data-testid=rec-panel]").GetAttribute("data-state").ShouldBe("closed");
        panel.Find("[data-testid=rec-allow-mic]").TextContent.ShouldContain("Allow microphone");
        await RecordingEngine.DidNotReceive().OpenInputAsync(Arg.Any<OpenInputRequest>());
        panel.FindAll("[data-testid=rec-device-select]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Allow_OpensTheInput_ListsDevices_AndAttachesTheInputMeter()
    {
        await Session.TogglePanelAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-allow-mic]").Click();

        panel.WaitForAssertion(() => panel.Find("[data-testid=rec-panel]").GetAttribute("data-permission").ShouldBe("granted"));
        await RecordingEngine.Received(1).OpenInputAsync(new OpenInputRequest(null, InputChannelModeDto.Mono1));
        await Engine.Received(1).AttachMeterAsync("input", "meter-input");
        panel.FindAll("[data-testid=rec-allow-mic]").ShouldBeEmpty();
        panel.FindAll("[data-testid=rec-device-select] option").Select(o => o.GetAttribute("value")).ShouldBe(["USB Mic", "Interface 1-2"]);
        var meter = panel.Find("[data-testid=meter-input]");
        (meter.GetAttribute("role"), meter.GetAttribute("aria-valuenow")).ShouldBe(("meter", "-60"));
    }

    [Fact]
    public async Task Granted_OpensTheStoredDeviceAtOnce()
    {
        StoredSettings("""{"deviceLabel":"Interface 1-2","inputMode":"stereo"}""");

        await OpenWithInputAsync();

        await RecordingEngine.Received(1).OpenInputAsync(new OpenInputRequest("d2", InputChannelModeDto.Stereo));
        RenderPanel().FindAll("[data-testid=rec-allow-mic]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Denied_ShowsTheReasonAndTryAgain()
    {
        Supported(MicrophoneAccess.Denied);
        await Session.TogglePanelAsync();
        var panel = RenderPanel();

        var unavailable = panel.Find("[data-testid=rec-unavailable]");
        unavailable.GetAttribute("data-reason").ShouldBe("denied");
        unavailable.TextContent.ShouldContain("address bar");
        Supported(MicrophoneAccess.Granted);
        panel.Find("[data-testid=rec-retry-permission]").Click();

        panel.WaitForAssertion(() => panel.FindAll("[data-testid=rec-unavailable]").ShouldBeEmpty());
        await RecordingEngine.Received(1).OpenInputAsync(Arg.Any<OpenInputRequest>());
    }

    [Theory]
    [InlineData("insecureContext", "insecure")]
    [InlineData("noAudioWorklet", "unsupported")]
    [InlineData("rateMismatch", "unsupported")]
    public async Task Unsupported_MapsTheEngineReason_WithoutTryAgain(string code, string reason)
    {
        Unsupported(code, "This browser cannot record here.");
        await Session.TogglePanelAsync();

        var panel = RenderPanel();

        panel.Find("[data-testid=rec-unavailable]").GetAttribute("data-reason").ShouldBe(reason);
        panel.FindAll("[data-testid=rec-retry-permission]").ShouldBeEmpty();
        panel.FindAll("[data-testid=rec-allow-mic]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("permissionDenied: Blocked.", "denied")]
    [InlineData("notFound: None.", "not-found")]
    [InlineData("busy: In use.", "busy")]
    [InlineData("unsupported: 48 kHz device.", "unsupported")]
    public async Task OpenRefused_ShowsTheDeviceState(string message, string reason)
    {
        RecordingEngine.OpenInputAsync(Arg.Any<OpenInputRequest>())
            .ThrowsAsync(RecordingException.FromEngine(message, new InvalidOperationException()));
        await Session.TogglePanelAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-allow-mic]").Click();

        panel.WaitForAssertion(() => panel.Find("[data-testid=rec-unavailable]").GetAttribute("data-reason").ShouldBe(reason));
    }

    [Fact]
    public async Task ModeUnavailable_KeepsThePanelAndExplains()
    {
        RecordingEngine.OpenInputAsync(Arg.Any<OpenInputRequest>())
            .ThrowsAsync(RecordingException.FromEngine("modeUnavailable: The device has one channel.", new InvalidOperationException()));
        await OpenWithInputAsync();

        Session.Input.Problem.ShouldBe("The device has one channel.");
        Session.Input.Unavailable.ShouldBeNull();
    }

    [Fact]
    public async Task InputLost_ShowsLostWithTryAgain()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();

        Callbacks.RaiseInputLost("ended");
        Refresh(panel);

        panel.Find("[data-testid=rec-unavailable]").GetAttribute("data-reason").ShouldBe("lost");
        panel.Find("[data-testid=rec-retry-permission]").ShouldNotBeNull();
    }

    [Fact]
    public async Task DeviceAndMode_ReopenTheInput()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-device-select]").Change("Interface 1-2");
        panel.Find("[data-testid=rec-input-mode]").Change("mono-2");

        await RecordingEngine.Received().OpenInputAsync(new OpenInputRequest("d2", InputChannelModeDto.Mono1));
        await RecordingEngine.Received().OpenInputAsync(new OpenInputRequest("d1", InputChannelModeDto.Mono2));
    }

    [Fact]
    public async Task PartMuteAndCountIn_AreStored()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-part-select]").Change("bass");
        panel.Find("[data-testid=rec-mute-original]").Change(false);
        panel.Find("[data-testid=rec-count-in]").Change("2");

        Session.Input.Settings.ShouldBe(RecordingSettings.Default with { DeviceLabel = "USB Mic", Part = RecordingPartDto.Bass, MuteOriginal = false, CountInBars = 2 });
        var stored = JSInterop.Invocations.Where(i => i.Identifier == "localStorage.setItem").Last().Arguments[1] as string;
        stored.ShouldNotBeNull().ShouldContain("\"countInBars\":2");
    }

    [Fact]
    public async Task PartsListEveryFamily_AndNoneDisablesMuteOriginal()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.FindAll("[data-testid=rec-part-select] option").Select(o => o.GetAttribute("value"))
            .ShouldBe(["vocals", "guitar", "bass", "piano", "drums", "other", "none"]);
        panel.Find("[data-testid=rec-part-select]").Change("none");

        panel.Find("[data-testid=rec-mute-original]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task Monitoring_SwitchesTheEngine_AndWarnsAboutHeadphones()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();
        panel.FindAll("[data-testid=rec-monitoring-warning]").ShouldBeEmpty();

        panel.Find("[data-testid=rec-monitoring]").Click();
        panel.Find("[data-testid=rec-monitoring-volume]").Change("40");

        panel.Find("[data-testid=rec-monitoring]").GetAttribute("aria-checked").ShouldBe("true");
        panel.Find("[data-testid=rec-monitoring-warning]").TextContent.ShouldContain("headphones");
        await RecordingEngine.Received().SetMonitoringAsync(new MonitoringSettings(true, 0.7));
        await RecordingEngine.Received().SetMonitoringAsync(new MonitoringSettings(true, 0.4));
    }

    [Theory]
    [InlineData(80, 0, false, "speed")]
    [InlineData(100, 2, false, "speed")]
    [InlineData(100, 0, true, "trainer")]
    public async Task Record_IsBlockedOutsideNormalSpeed(int speed, int transpose, bool trainer, string reason)
    {
        Host.Practice = PracticeSettingsDto.Default with
        {
            SpeedPercent = speed,
            TransposeSemitones = transpose,
            Trainer = SpeedTrainerSettingsDto.Default with { Enabled = trainer },
        };
        await OpenWithInputAsync();

        var panel = RenderPanel();

        panel.Find("[data-testid=rec-record]").HasAttribute("disabled").ShouldBeTrue();
        panel.Find("[data-testid=rec-record-disabled-reason]").GetAttribute("data-reason").ShouldBe(reason);
        panel.FindAll("[data-testid=rec-set-normal-speed]").Count.ShouldBe(reason == "speed" ? 1 : 0);
    }

    [Fact]
    public async Task SetToNormalSpeed_CallsTheMixer_AndEnablesRecord()
    {
        Host.Practice = PracticeSettingsDto.Default with { SpeedPercent = 80 };
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-record-disabled-reason]").TextContent.ShouldContain("100 % speed");
        panel.Find("[data-testid=rec-set-normal-speed]").Click();

        Host.NormalSpeedCalls.ShouldBe(1);
        Refresh(panel);
        panel.Find("[data-testid=rec-record]").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task Record_IsBlockedWithoutInput_TrackOrRoomForTakes()
    {
        await Session.TogglePanelAsync();
        RenderPanel().Find("[data-testid=rec-record-disabled-reason]").GetAttribute("data-reason").ShouldBe("input");

        Host.TrackReady = false;
        Session.Block!.Reason.ShouldBe("track");

        Host.TrackReady = true;
        Supported(MicrophoneAccess.Granted);
        Api.ListTakesAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(RecordingData.List([.. Enumerable.Range(1, 200).Select(n => RecordingData.Take(Guid.NewGuid(), number: n))]));
        await Session.TogglePanelAsync();
        await Session.TogglePanelAsync();
        Session.Block!.Reason.ShouldBe("limit");
    }

    [Fact]
    public async Task Record_StartsWithTheChosenOptions_AndShowsTheLiveState()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-record]").Click();
        Callbacks.RaiseStateChanged("countIn");
        Callbacks.RaiseProgress(new RecordingProgress(0, null, 3));
        Refresh(panel);

        await RecordingEngine.Received(1).StartRecordingAsync(new RecordOptions
        {
            LatencySamples = 1500,
            LatencyMethod = LatencyMethodDto.Estimated,
            Part = RecordingPartDto.Vocals,
            MuteOriginal = true,
            CountInBars = 1,
        });
        panel.Find("[data-testid=rec-panel]").GetAttribute("data-state").ShouldBe("countIn");
        var status = panel.Find("[data-testid=rec-status]");
        (status.GetAttribute("aria-live"), status.TextContent).ShouldBe(("polite", "Count-in 3"));

        Callbacks.RaiseStateChanged("recording");
        Callbacks.RaiseProgress(new RecordingProgress(61.55, null, null));
        Refresh(panel);

        var record = panel.Find("[data-testid=rec-record]");
        (record.QuerySelector(".rz-button-text")!.TextContent, record.GetAttribute("aria-pressed")).ShouldBe(("Stop", "true"));
        panel.Find("[data-testid=rec-elapsed]").TextContent.ShouldBe("1:01.5");
        panel.Find("[data-testid=rec-status]").TextContent.ShouldBe("Recording");
        panel.Find("[data-testid=rec-device-select]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task PartNone_NeverMutesTheOriginal()
    {
        StoredSettings("""{"part":"none","muteOriginal":true}""");
        await OpenWithInputAsync();

        await Session.ToggleRecordAsync();

        await RecordingEngine.Received(1).StartRecordingAsync(Arg.Is<RecordOptions>(o => o.Part == RecordingPartDto.None && !o.MuteOriginal));
    }

    [Fact]
    public async Task StartRefused_ShowsTheError()
    {
        RecordingEngine.StartRecordingAsync(Arg.Any<RecordOptions>())
            .ThrowsAsync(RecordingException.FromEngine("recordingSpeed: Set the speed to 100 %.", new InvalidOperationException()));
        await OpenWithInputAsync();
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-record]").Click();

        panel.WaitForAssertion(() => panel.Find("[data-testid=rec-error]").TextContent.ShouldBe("Set the speed to 100 %."));
    }

    [Fact]
    public async Task LoopPass_IsShownFromAdvanced()
    {
        await OpenWithInputAsync();
        Callbacks.RaiseStateChanged("recording");
        Callbacks.RaiseProgress(new RecordingProgress(5, 2, null));

        RenderPanel(ExperienceLevel.Advanced).Find("[data-testid=rec-pass]").GetAttribute("data-pass").ShouldBe("2");
        RenderPanel(ExperienceLevel.Beginner).FindAll("[data-testid=rec-pass]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner, false, false)]
    [InlineData(ExperienceLevel.Advanced, true, false)]
    [InlineData(ExperienceLevel.Expert, true, true)]
    public async Task Levels_FollowR10(ExperienceLevel level, bool advanced, bool expert)
    {
        await OpenWithInputAsync();

        var panel = RenderPanel(level);

        panel.FindAll("[data-testid=rec-input-mode]").Count.ShouldBe(advanced ? 1 : 0);
        panel.FindAll("[data-testid=rec-monitoring-volume]").Count.ShouldBe(advanced ? 1 : 0);
        panel.FindAll("[data-testid=rec-latency-samples]").Count.ShouldBe(expert ? 1 : 0);
        panel.FindAll("[data-testid=meter-db-input]").Count.ShouldBe(expert ? 1 : 0);
        foreach (var id in new[] { "rec-device-select", "rec-part-select", "rec-mute-original", "rec-count-in", "rec-monitoring", "rec-calibrate", "rec-record" })
        {
            panel.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public async Task LatencyStatus_ShowsTheEstimate_ThenTheCalibration_AndASetupChange()
    {
        await OpenWithInputAsync();
        var status = RenderPanel(ExperienceLevel.Expert).Find("[data-testid=rec-latency-status]");
        (status.GetAttribute("data-method"), status.TextContent).ShouldBe(("estimated", "Estimated latency: 34.0 ms (calibrate for exact timing)"));

        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([RecordingData.Calibration(outputLatency: 300 + 221)]);
        await Session.TogglePanelAsync();
        await Session.TogglePanelAsync();
        var panel = RenderPanel(ExperienceLevel.Expert);

        panel.Find("[data-testid=rec-latency-status]").GetAttribute("data-method").ShouldBe("loopback");
        panel.Find("[data-testid=rec-latency-samples]").TextContent.ShouldBe("1102 samples");
        panel.Find("[data-testid=rec-setup-changed]").TextContent.ShouldContain("calibrate again");
    }

    [Fact]
    public async Task ManualCalibration_IsLabelledManual_AndASmallChangeIsNoSetupChange()
    {
        var manual = RecordingData.Calibration(outputLatency: 300 + 220) with { Method = CalibrationMethodDto.Manual };
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([manual]);
        await OpenWithInputAsync();

        var panel = RenderPanel();

        panel.Find("[data-testid=rec-latency-status]").GetAttribute("data-method").ShouldBe("manual");
        panel.FindAll("[data-testid=rec-setup-changed]").ShouldBeEmpty();
        Session.LatencyInUse!.CalibrationId.ShouldBe(RecordingData.CalibrationId);
    }

    [Fact]
    public async Task EveryControl_HasAnAccessibleNameContainingItsVisibleText()
    {
        await OpenWithInputAsync();

        var panel = RenderPanel(ExperienceLevel.Expert);

        foreach (var control in panel.FindAll("button, select, input"))
        {
            var visible = control.TextContent.Trim();
            var label = control.GetAttribute("aria-label");
            var id = control.GetAttribute("id");
            var labelled = id is not null && panel.FindAll($"label[for={id}]").Count > 0;
            (label is not null || visible.Length > 0 || labelled).ShouldBeTrue($"{control.GetAttribute("data-testid")} has no name");
            if (label is not null && visible.Length > 0 && control.TagName == "BUTTON")
            {
                label.ShouldContain(visible, Case.Insensitive, control.GetAttribute("data-testid"));
            }
        }
    }

    [Fact]
    public async Task ClosingThePanel_ReleasesTheMicrophone()
    {
        await OpenWithInputAsync();

        await Session.TogglePanelAsync();

        await RecordingEngine.Received(1).CloseInputAsync();
        Session.PanelOpen.ShouldBeFalse();
        Session.Input.IsOpen.ShouldBeFalse();
    }
}
