using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Components.Recording;
using StemStudio.Web.Recording;
using static StemStudio.Web.Tests.Recording.RecordingData;

namespace StemStudio.Web.Tests.Recording;

/// <summary>015 T042: captures -> streamed uploads -> polling -> saved FLAC in the strip; the take strip (FR-011, FR-012).</summary>
public sealed class TakeUploaderTests : RecordingTestContext
{
    private static readonly Guid SecondId = Guid.Parse("5a1f0c3b-9e01-4a8e-9d55-3f2b8a512c1e");

    public TakeUploaderTests()
    {
        var wav = Wav(4096);
        RecordingEngine.CaptureWavAsync(Arg.Any<string>()).Returns(wav);
        Api.CreateTakeAsync(TrackId, Arg.Any<Stream>(), Arg.Any<RecordingTakeUploadDto>(), Arg.Any<CancellationToken>())
            .Returns(Take(status: TakeStatusDto.Saving), Take(SecondId, TakeStatusDto.Saving, 2));
    }

    [Fact]
    public async Task Stop_UploadsTheCapture_WithItsMetadata_AndPlaysItLocallyAtOnce()
    {
        Host.MixId = Guid.NewGuid();
        await RecordAndStopAsync(Capture("c1"));

        var item = Session.Takes.Items.ShouldHaveSingleItem();
        item.Status.ShouldBe("saving");
        await RecordingEngine.Received(1).LoadTakeAsync(new LoadTakeRequest("c1", TakeSource.FromCapture("c1"), RecordingPartDto.Vocals));
        await Api.Received(1).CreateTakeAsync(TrackId, Arg.Any<Stream>(), Arg.Is<RecordingTakeUploadDto>(m =>
            m.SessionId == SessionId && m.StartSample == 441_000 && m.LengthSamples == 352_800 && m.LatencySamples == 1102 &&
            m.DeviceLabel == "USB Mic" && m.Part == RecordingPartDto.Vocals && m.MixId == Host.MixId && m.Speed == 1.0 && m.TransposeSemitones == 0),
            Arg.Any<CancellationToken>());
        Session.StatusText.ShouldBe("Saving 1 take");
        Session.SavingPercent.ShouldBe(0);
    }

    [Fact]
    public async Task TheWav_IsReadWithTheBodyLimit()
    {
        var wav = Wav();
        RecordingEngine.CaptureWavAsync("c1").Returns(wav);

        await RecordAndStopAsync(Capture("c1"));

        await wav.Received(1).OpenReadStreamAsync(256L * 1024 * 1024, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoopPasses_AreUploadedOneAfterTheOther_InCaptureOrder()
    {
        await RecordAndStopAsync(Capture("c1", pass: 1), Capture("c2", pass: 2, partial: true));

        Received.InOrder(() =>
        {
            RecordingEngine.CaptureWavAsync("c1");
            RecordingEngine.CaptureWavAsync("c2");
        });
        Session.Takes.Items.Count.ShouldBe(2);
        await RecordingEngine.Received(1).LoadTakeAsync(Arg.Is<LoadTakeRequest>(r => r.TakeId == "c2"));
        Session.StatusText.ShouldBe("Saving 2 takes");
    }

    [Fact]
    public async Task TheCallbackAndTheStopResult_AreHandledOnce()
    {
        await RecordAndStopAsync(Capture("c1"));

        Callbacks.RaiseTakesCaptured([Capture("c1")]);

        await Api.Received(1).CreateTakeAsync(Arg.Any<Guid>(), Arg.Any<Stream>(), Arg.Any<RecordingTakeUploadDto>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakesCapturedCallback_AloneAlsoUploads()
    {
        await OpenWithInputAsync();

        Callbacks.RaiseTakesCaptured([Capture("c9")]);

        await RecordingEngine.Received(1).CaptureWavAsync("c9");
    }

    [Fact]
    public async Task Polling_SwitchesTheStripToTheSavedFlac_AndReleasesTheCapture()
    {
        Api.GetTakeAsync(TakeId, Arg.Any<CancellationToken>()).Returns(Take(status: TakeStatusDto.Saving), Take());
        await RecordAndStopAsync(Capture("c1"));

        await TickAsync();
        Session.Takes.Items[0].Status.ShouldBe("saving");
        await TickAsync();

        await RecordingEngine.Received(1).LoadTakeAsync(new LoadTakeRequest(
            TakeId.ToString("D"), TakeSource.FromUrl($"http://api.test/api/takes/{TakeId:D}/audio", 441_000, 352_800, 1), RecordingPartDto.Guitar));
        await RecordingEngine.Received(1).ReleaseCaptureAsync("c1");
        Session.Takes.Items[0].Capture.ShouldBeNull();
        Session.StatusText.ShouldBe("Saved");
        Session.SavingPercent.ShouldBeNull();
    }

    [Fact]
    public async Task RefusedUpload_ShowsTheError_AndSaveAgainRetries()
    {
        Api.CreateTakeAsync(TrackId, Arg.Any<Stream>(), Arg.Any<RecordingTakeUploadDto>(), Arg.Any<CancellationToken>())
            .Returns<RecordingTakeDto>(_ => throw Problem(System.Net.HttpStatusCode.UnprocessableEntity, "The take is silent"), _ => Take(status: TakeStatusDto.Saving));
        await RecordAndStopAsync(Capture("c1"));
        var panel = RenderPanel();

        panel.Find("[data-testid=rec-error]").TextContent.ShouldContain("silent");
        Session.Takes.Items[0].Status.ShouldBe("local");
        panel.Find("[data-testid=rec-save-again]").Click();

        panel.WaitForAssertion(() => Session.Takes.Items[0].Status.ShouldBe("saving"));
        Refresh(panel);
        panel.FindAll("[data-testid=rec-save-again]").ShouldBeEmpty();
    }

    [Fact]
    public async Task UnreadableCapture_IsAnUploadError()
    {
        RecordingEngine.CaptureWavAsync("c1").ThrowsAsync(RecordingException.FromEngine("unknown capture", new InvalidOperationException()));

        await RecordAndStopAsync(Capture("c1"));

        Session.UploadError.ShouldBe("The take could not be read from the browser: unknown capture");
        Session.CanSaveAgain.ShouldBeTrue();
    }

    [Fact]
    public async Task SavingProgress_CountsTheFinishedTakesOfTheSession()
    {
        Api.GetTakeAsync(TakeId, Arg.Any<CancellationToken>()).Returns(Take());
        Api.GetTakeAsync(SecondId, Arg.Any<CancellationToken>()).Returns(Take(SecondId, TakeStatusDto.Saving, 2));
        await RecordAndStopAsync(Capture("c1", 1), Capture("c2", 2));

        await TickAsync();

        Session.SavingPercent.ShouldBe(50);
        RenderPanel().Find("[data-testid=rec-saving]").GetAttribute("aria-valuenow").ShouldBe("50");
    }

    [Fact]
    public async Task StopFailing_ShowsTheError()
    {
        await OpenWithInputAsync();
        Callbacks.RaiseStateChanged("recording");
        RecordingEngine.StopRecordingAsync().ThrowsAsync(RecordingException.FromEngine("Stop failed", new InvalidOperationException()));

        await Session.ToggleRecordAsync();

        Session.Error.ShouldBe("Stop failed");
    }

    [Fact]
    public async Task TakeStrip_HasTheTakeIds_AndSendsItsSettingsToTheTakeChannel()
    {
        await RecordAndStopAsync(Capture("c1"));
        var strip = RenderAtLevel<TakeStrip>(StemStudio.Web.Experience.ExperienceLevel.Expert, p => p.Add(c => c.Takes, Session.Takes));

        var root = strip.Find("[data-testid=strip-take]");
        root.GetAttribute("data-take-id").ShouldBe(TakeId.ToString("D"));
        foreach (var id in new[] { "fader-take", "pan-take", "mute-take", "solo-take", "meter-take", "fader-value-take", "pan-value-take", "meter-db-take" })
        {
            strip.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        strip.Find("[data-testid=fader-take]").Input("50");
        strip.Find("[data-testid=mute-take]").Click();

        await Engine.Received().SetChannelAsync(new ChannelSettingsDto("take", 0.5, 0, false, false));
        await Engine.Received().SetChannelAsync(new ChannelSettingsDto("take", 0.5, 0, true, false));
        await Engine.Received().AttachMeterAsync("take", "meter-take");
    }

    [Fact]
    public void TakeStrip_IsAbsentWithoutALoadedTake() =>
        Render<TakeStrip>(p => p.Add(c => c.Takes, Session.Takes)).FindAll("[data-testid=strip-take]").ShouldBeEmpty();

    [Fact]
    public async Task FailingTakeLoad_KeepsTheStripHidden_AndExplains()
    {
        RecordingEngine.LoadTakeAsync(Arg.Any<LoadTakeRequest>()).ThrowsAsync(RecordingException.FromEngine("decode failed", new InvalidOperationException()));

        await RecordAndStopAsync(Capture("c1"));

        Session.Takes.Loaded.ShouldBeNull();
        Session.Takes.Error.ShouldBe("The take could not be loaded: decode failed");
    }

    [Fact]
    public async Task MixSettings_CarryTheSavedTakeAndTheStrip()
    {
        await RecordAndStopAsync(Capture("c1"));
        await Session.Takes.ChangeStripAsync(s => s.Pan = -0.5);

        Session.Takes.MixSettings().ShouldBe(new TakeChannelSettingsDto { TakeId = TakeId, Volume = 0.8, Pan = -0.5, Mute = false, Solo = false });
    }

    [Fact]
    public async Task AfterTheUpload_TheStripAnnouncesTheSavedTake_SoTheMixCanKeepIt()
    {
        // r14 E2E: a mix saved while the take was still being rendered lost its take channel (FR-012), because the strip
        // was only announced when the capture was loaded (no server id yet) and again when the take was ready.
        var announced = new List<TakeChannelSettingsDto?>();
        Session.Takes.StripChanged += () => announced.Add(Session.Takes.MixSettings());

        await RecordAndStopAsync(Capture("c1"));

        announced.ShouldContain(t => t != null && t.TakeId == TakeId);
    }

    [Fact]
    public async Task SavedTakes_AreListedNewestFirst()
    {
        var older = Take(SecondId, number: 2) with { RecordedAt = new DateTimeOffset(2026, 11, 16, 9, 0, 0, TimeSpan.Zero) };
        Api.ListTakesAsync(TrackId, Arg.Any<CancellationToken>()).Returns(List(older, Take()));

        await OpenWithInputAsync();

        Session.Takes.Items.Select(i => i.Id).ShouldBe([TakeId.ToString("D"), SecondId.ToString("D")]);
        Session.Takes.TotalSizeBytes.ShouldBe(2_400_000);
    }

    private async Task RecordAndStopAsync(params CapturedTake[] takes)
    {
        await OpenWithInputAsync();
        await Session.ToggleRecordAsync();
        Callbacks.RaiseStateChanged("recording");
        RecordingEngine.StopRecordingAsync().Returns(takes);
        await Session.ToggleRecordAsync();
        Callbacks.RaiseStateChanged("open");
    }
}
