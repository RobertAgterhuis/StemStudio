using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Web.Components.Voices;
using StemStudio.Web.Interop;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Voices;

/// <summary>The in-app recorder (010 T050, FR-005; ui-test-ids 1.9.0 voice-record-*): JS interop mocked.</summary>
public sealed class VoiceRecorderTests : PageTestContext
{
    private readonly IVoiceRecording _recording = Substitute.For<IVoiceRecording>();

    public VoiceRecorderTests()
    {
        Recorder.StartAsync(60).Returns(_recording);
        _recording.StopAsync().Returns(new RecordedSample([1, 2, 3], "audio/webm;codecs=opus", "blob:recording"));
    }

    private RecordedSample? Recorded { get; set; }

    private IRenderedComponent<VoiceRecorder> RenderRecorder() =>
        Render<VoiceRecorder>(p => p.Add(c => c.OnRecorded, s => Recorded = s));

    [Fact]
    public async Task StartAndStop_RecordASample_WhileTheTimerRuns()
    {
        var recorder = RenderRecorder();
        recorder.Find("[data-testid=voice-record-timer]").TextContent.ShouldBe("0:00");
        recorder.FindAll("[data-testid=voice-record-stop]").ShouldBeEmpty();

        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());
        Time.Advance(TimeSpan.FromSeconds(15));
        Timer.Tick();

        recorder.WaitForAssertion(() => recorder.Find("[data-testid=voice-record-timer]").TextContent.ShouldBe("0:15"));
        recorder.Find("[data-testid=voice-record-start]").HasAttribute("disabled").ShouldBeTrue();
        await recorder.Find("[data-testid=voice-record-stop]").ClickAsync(new());

        Recorded!.Bytes.ShouldBe([1, 2, 3]);
        recorder.Find("[data-testid=voice-record-status]").TextContent.ShouldContain("Recorded 0:15");
        await _recording.DidNotReceive().DisposeAsync();
    }

    [Fact]
    public async Task Recording_StopsByItself_AfterSixtySeconds()
    {
        var recorder = RenderRecorder();

        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());
        Time.Advance(TimeSpan.FromSeconds(60));
        Timer.Tick();

        recorder.WaitForAssertion(() => Recorded.ShouldNotBeNull());
        recorder.Find("[data-testid=voice-record-timer]").TextContent.ShouldBe("1:00");
        recorder.FindAll("[data-testid=voice-record-stop]").ShouldBeEmpty();
    }

    [Fact]
    public void AnUnsupportedBrowser_ShowsTheReason_AndDisablesRecording()
    {
        Recorder.UnavailableReasonAsync().Returns("This browser cannot record from a microphone.");

        var recorder = RenderRecorder();

        var notice = recorder.Find("[data-testid=voice-record-unavailable]");
        notice.TextContent.ShouldContain("This browser cannot record from a microphone.");
        notice.TextContent.ShouldContain("upload");
        recorder.Find("[data-testid=voice-record-start]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task ADeniedMicrophone_ShowsTheReason()
    {
        Recorder.StartAsync(60).ThrowsAsync(new VoiceRecorderException("Microphone permission was denied. You can upload a recording instead."));
        var recorder = RenderRecorder();

        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());

        recorder.Find("[data-testid=voice-record-unavailable]").TextContent.ShouldContain("permission was denied");
        Recorded.ShouldBeNull();
    }

    [Fact]
    public async Task AFailedStop_IsShown()
    {
        _recording.StopAsync().ThrowsAsync(new VoiceRecorderException("The recording could not be read."));
        var recorder = RenderRecorder();

        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());
        await recorder.Find("[data-testid=voice-record-stop]").ClickAsync(new());

        recorder.Find("[data-testid=voice-record-unavailable]").TextContent.ShouldContain("could not be read");
    }

    [Fact]
    public async Task LeavingThePage_ReleasesTheMicrophone()
    {
        var recorder = RenderRecorder();
        await recorder.Find("[data-testid=voice-record-start]").ClickAsync(new());

        await DisposeComponentsAsync();

        await _recording.Received(1).DisposeAsync();
    }

    [Fact]
    public void TheButtons_AreNamedAndTheTimerIsAnnounced()
    {
        var recorder = RenderRecorder();

        recorder.Find("[data-testid=voice-record-start]").TextContent.ShouldContain("Record");
        var timer = recorder.Find("[data-testid=voice-record-timer]");
        (timer.GetAttribute("role"), timer.GetAttribute("aria-label")).ShouldBe(("timer", "Recording time"));
    }
}
