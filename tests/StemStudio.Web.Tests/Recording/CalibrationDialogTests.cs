using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Experience;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T049: the calibration dialog (FR-007, FR-020; ui-test-ids 1.14.0 cal-*, rec-latency-status, rec-setup-changed).</summary>
public sealed class CalibrationDialogTests : RecordingTestContext
{
    private static readonly CalibrationResult Ok = new(true, 1102, 25.0, 8, 4, 300, -12, null, []);

    public CalibrationDialogTests()
    {
        Api.UpsertCalibrationAsync(Arg.Any<InputCalibrationInput>(), Arg.Any<CancellationToken>())
            .Returns(call => Calibration(samples: call.Arg<InputCalibrationInput>().LatencySamples) with
            {
                Method = call.Arg<InputCalibrationInput>().Method,
            });
    }

    [Fact]
    public async Task OpensFromThePanel_WithInstructions_AndCloses()
    {
        await OpenWithInputAsync();
        var panel = RenderPanel(ExperienceLevel.Beginner);

        panel.Find("[data-testid=rec-calibrate]").Click();

        var dialog = panel.Find("[data-testid=cal-dialog]");
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal")).ShouldBe(("dialog", "true"));
        panel.Find("[data-testid=cal-instructions]").TextContent.ShouldContain("cable");
        panel.Find("[data-testid=cal-instructions]").TextContent.ShouldContain("USB Mic");
        panel.Find("[data-testid=cal-cancel]").Click();
        panel.FindAll("[data-testid=cal-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Escape_ClosesTheDialog()
    {
        var closed = 0;
        var dialog = await RenderDialogAsync(ExperienceLevel.Advanced, () => closed++);

        dialog.Find("[data-testid=cal-dialog]").KeyDown("Escape");

        closed.ShouldBe(1);
    }

    [Fact]
    public async Task WhileMeasuring_ShowsTheBusyState()
    {
        var pending = new TaskCompletionSource<CalibrationResult>();
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).Returns(pending.Task);
        var dialog = await RenderDialogAsync();

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.Find("[data-testid=cal-running]").GetAttribute("aria-busy").ShouldBe("true");
        dialog.Find("[data-testid=cal-start]").HasAttribute("disabled").ShouldBeTrue();
        pending.SetResult(Ok);
        dialog.WaitForAssertion(() => dialog.FindAll("[data-testid=cal-running]").ShouldBeEmpty());
    }

    [Fact]
    public async Task Success_ShowsTheResult_AndStoresItForTheInput()
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).Returns(Ok);
        var dialog = await RenderDialogAsync(ExperienceLevel.Expert);

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[data-testid=cal-result]").GetAttribute("data-ok").ShouldBe("true"));
        dialog.Find("[data-testid=cal-result-ms]").TextContent.ShouldBe("25.0 ms");
        dialog.Find("[data-testid=cal-result-samples]").TextContent.ShouldContain("1102");
        dialog.Find("[data-testid=cal-result-clicks]").TextContent.ShouldBe("8 clicks found");
        dialog.Find("[data-testid=cal-result-spread]").TextContent.ShouldBe("spread 0.1 ms");
        await Api.Received(1).UpsertCalibrationAsync(Arg.Is<InputCalibrationInput>(i =>
            i.DeviceLabel == "USB Mic" && i.LatencySamples == 1102 && i.Method == CalibrationMethodDto.Loopback && i.SpreadSamples == 4 &&
            i.DetectedClicks == 8 && i.OutputLatencySamples == 300 && i.DeviceIdHint == "d1"), Arg.Any<CancellationToken>());
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Loopback);
        dialog.Find("[data-testid=cal-delete]").ShouldNotBeNull();
    }

    [Fact]
    public async Task SamplesAreExpertOnly()
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).Returns(Ok);
        var dialog = await RenderDialogAsync(ExperienceLevel.Advanced);

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[data-testid=cal-result-ms]"));
        dialog.FindAll("[data-testid=cal-result-samples]").ShouldBeEmpty();
    }

    [Theory]
    [InlineData("noSignal", "cable")]
    [InlineData("tooFewClicks", "gain up")]
    [InlineData("inconsistent", "different times")]
    [InlineData("tooLong", "longer than one second")]
    public async Task Failure_ShowsTheReasonWithItsTip_AndTryAgain(string reason, string tip)
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>())
            .Returns(new CalibrationResult(false, null, null, 2, null, 300, -60, reason, []), Ok);
        var dialog = await RenderDialogAsync();

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[data-testid=cal-result]").GetAttribute("data-ok").ShouldBe("false"));
        var shown = dialog.Find("[data-testid=cal-reason]");
        (shown.GetAttribute("data-reason"), shown.TextContent.Contains(tip, StringComparison.Ordinal)).ShouldBe((reason, true));
        await Api.DidNotReceive().UpsertCalibrationAsync(Arg.Any<InputCalibrationInput>(), Arg.Any<CancellationToken>());
        dialog.Find("[data-testid=cal-try-again]").Click();
        dialog.WaitForAssertion(() => dialog.Find("[data-testid=cal-result]").GetAttribute("data-ok").ShouldBe("true"));
    }

    [Fact]
    public async Task ClippedClicks_AreMentioned()
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).Returns(Ok with { Warnings = ["clipped"] });
        var dialog = await RenderDialogAsync();

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[data-testid=cal-result]").TextContent.ShouldContain("clipped"));
    }

    [Fact]
    public async Task RefusedWhilePlaying_ShowsTheEngineSentence()
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).ThrowsAsync(new RecordingException("Pause playback to calibrate"));
        var dialog = await RenderDialogAsync();

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[role=alert]").TextContent.ShouldBe("Pause playback to calibrate"));
    }

    [Fact]
    public async Task ManualEntry_IsAdvanced_RoundsToSamples_AndIsUsedAsManual()
    {
        var dialog = await RenderDialogAsync(ExperienceLevel.Advanced);

        dialog.Find("[data-testid=cal-manual]").Click();
        var field = dialog.Find("[data-testid=cal-manual-ms]");
        (field.GetAttribute("min"), field.GetAttribute("max"), field.GetAttribute("step")).ShouldBe(("0", "1000", "0.1"));
        field.Change("12.34");
        dialog.Find("[data-testid=cal-manual-slider]").GetAttribute("aria-valuenow").ShouldBe("12.3");
        dialog.Find("[data-testid=cal-save]").Click();

        await Api.Received(1).UpsertCalibrationAsync(Arg.Is<InputCalibrationInput>(i =>
            i.Method == CalibrationMethodDto.Manual && i.LatencySamples == 542 && i.OutputLatencySamples == 300), Arg.Any<CancellationToken>());
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Manual);
    }

    [Fact]
    public async Task ManualEntry_ClampsTo1000AndStartsAtTheStoredValue()
    {
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([Calibration()]);
        var dialog = await RenderDialogAsync(ExperienceLevel.Advanced);

        dialog.Find("[data-testid=cal-manual]").Click();
        dialog.Find("[data-testid=cal-manual-ms]").GetAttribute("value").ShouldBe("25.0");
        dialog.Find("[data-testid=cal-manual-slider]").Input("1500");

        dialog.Find("[data-testid=cal-manual-ms]").GetAttribute("value").ShouldBe("1000.0");
    }

    [Fact]
    public async Task ManualEntry_IsHiddenAtBeginner()
    {
        var dialog = await RenderDialogAsync(ExperienceLevel.Beginner);

        dialog.FindAll("[data-testid=cal-manual]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_ForgetsTheCalibration_AndTheEstimateIsUsedAgain()
    {
        Api.ListCalibrationsAsync(Arg.Any<CancellationToken>()).Returns([Calibration()]);
        var dialog = await RenderDialogAsync();
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Loopback);

        dialog.Find("[data-testid=cal-delete]").Click();

        dialog.WaitForAssertion(() => dialog.FindAll("[data-testid=cal-delete]").ShouldBeEmpty());
        Session.LatencyInUse!.Method.ShouldBe(LatencyMethodDto.Estimated);
    }

    [Fact]
    public async Task FailedSave_IsShown()
    {
        RecordingEngine.CalibrateLatencyAsync(Arg.Any<CalibrationOptions?>()).Returns(Ok);
        Api.UpsertCalibrationAsync(Arg.Any<InputCalibrationInput>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(Problem(System.Net.HttpStatusCode.UnprocessableEntity, "The latency is out of range"));
        var dialog = await RenderDialogAsync();

        dialog.Find("[data-testid=cal-start]").Click();

        dialog.WaitForAssertion(() => dialog.Find("[role=alert]").TextContent.ShouldContain("out of range"));
    }

    [Fact]
    public void Texts_CoverTheFallbackTip_AndEmptyMs()
    {
        CalibrationRun.Tip(null).ShouldContain("Try again");
        CalibrationRun.Ms(null).ShouldBeEmpty();
    }

    [Fact]
    public async Task NoCalibrationWithoutAnInput()
    {
        await Session.TogglePanelAsync();

        Session.NewCalibration().ShouldBeNull();
    }

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> RenderDialogAsync(
        ExperienceLevel level = ExperienceLevel.Advanced, Action? onClose = null)
    {
        await OpenWithInputAsync();
        var run = Session.NewCalibration()!;
        return RenderAtLevel<CalibrationDialog>(level, p => p.Add(c => c.Run, run).Add(c => c.OnClose, () => onClose?.Invoke()));
    }
}
