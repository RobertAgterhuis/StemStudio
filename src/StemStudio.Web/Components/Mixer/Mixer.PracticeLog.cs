using StemStudio.Contracts.Practice;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Practice;

namespace StemStudio.Web.Components.Mixer;

/// <summary>
/// Practice log of the mixer page (014 US6, FR-025..FR-029): the <see cref="PracticeRecorder"/> fed with the engine's
/// playback state, positions (with the heard speed), loop passes and the trainer state while the log is on; closed when
/// the page goes away. The panel shows the log.
/// </summary>
public sealed partial class Mixer
{
    private PracticeRecorder? _recorder;

    private bool ShowPracticeLog { get; set; }

    private PracticeLogSettingsDto? LogSettings { get; set; }

    private void TogglePracticeLog() => ShowPracticeLog = !ShowPracticeLog;

    // Starts recording when the log is on (a 409 practice-log-off later stops it on its own).
    private async Task StartRecorderAsync()
    {
        try
        {
            LogSettings = await Api.GetPracticeLogSettingsAsync();
        }
        catch (ApiException)
        {
            LogSettings = null;
        }

        if (LogSettings?.Enabled != true)
        {
            return;
        }

        _recorder = new PracticeRecorder((id, session, ct) => Api.UpsertPracticeSessionAsync(id, session, ct), Clock);
        _recorder.Begin(Id, State!.MixId, State.Sections);
        Engine.LoopPassed += OnLoopPassed;
    }

    private void OnLogSettingsChanged(PracticeLogSettingsDto settings)
    {
        var wasOn = LogSettings?.Enabled == true;
        LogSettings = settings;
        if (settings.Enabled && (!wasOn || _recorder?.State == PracticeRecorderState.Off))
        {
            Engine.LoopPassed -= OnLoopPassed;
            _ = StartRecorderAsync();
        }
    }

    private void RecordPlayback(PlaybackState state) => _ = _recorder?.OnPlaybackAsync(state, Position);

    private void RecordPosition(double seconds) => _ = _recorder?.OnPositionAsync(seconds, PracticeLive.SpeedPercent);

    private void RecordContext()
    {
        if (_recorder is not null && State is not null)
        {
            _recorder.UpdateSections(State.Sections);
            _recorder.SetMix(State.MixId);
        }
    }

    private void OnLoopPassed(object? sender, LoopPassInfo pass) =>
        _ = InvokeAsync(() => _recorder?.OnLoopPass(pass, State?.Practice.Trainer.Enabled == true, State?.Loop?.SectionId));

    private async Task CloseRecorderAsync()
    {
        Engine.LoopPassed -= OnLoopPassed;
        await (_recorder?.CloseAsync() ?? Task.CompletedTask);
    }
}
