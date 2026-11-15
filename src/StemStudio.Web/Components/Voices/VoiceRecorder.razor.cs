using Microsoft.AspNetCore.Components;
using StemStudio.Web.Interop;
using StemStudio.Web.Voices;

namespace StemStudio.Web.Components.Voices;

/// <summary>Runs a recording through <see cref="IVoiceRecorder"/> and keeps the timer on the <see cref="TimeProvider"/> (feature 010).</summary>
public sealed partial class VoiceRecorder
{
    private IVoiceRecording? _session;
    private IVoiceRecording? _last;
    private DateTimeOffset _startedAt;

    [Inject]
    private IVoiceRecorder Recorder { get; set; } = default!;

    [Inject]
    private TimeProvider Time { get; set; } = default!;

    /// <summary>Raised with the finished recording.</summary>
    [Parameter]
    public EventCallback<RecordedSample> OnRecorded { get; set; }

    private bool IsRecording => _session is not null;

    private double Elapsed { get; set; }

    private double? RecordedSeconds { get; set; }

    private string? Unavailable { get; set; }

    /// <inheritdoc />
    protected override bool NeedsPolling => IsRecording;

    /// <inheritdoc />
    protected override async Task OnInitializedAsync() => Unavailable = await Recorder.UnavailableReasonAsync();

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
    {
        Elapsed = Math.Min((Time.GetUtcNow() - _startedAt).TotalSeconds, VoiceRules.MaxRecordingSeconds);
        if (Elapsed >= VoiceRules.MaxRecordingSeconds)
        {
            await StopAsync();
        }
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _ = (_session ?? _last)?.DisposeAsync().AsTask();
        }

        base.Dispose(disposing);
    }

    private async Task StartAsync()
    {
        try
        {
            await ReleaseLastAsync();
            _session = await Recorder.StartAsync(VoiceRules.MaxRecordingSeconds);
            (_startedAt, Elapsed, RecordedSeconds) = (Time.GetUtcNow(), 0, null);
            StartPolling();
        }
        catch (VoiceRecorderException ex)
        {
            Unavailable = ex.Message;
        }
    }

    private async Task StopAsync()
    {
        (_last, _session) = (_session, null);
        try
        {
            var sample = await _last!.StopAsync();
            RecordedSeconds = Elapsed;
            await OnRecorded.InvokeAsync(sample);
        }
        catch (VoiceRecorderException ex)
        {
            Unavailable = ex.Message;
        }
    }

    private async Task ReleaseLastAsync()
    {
        if (_last is { } last)
        {
            _last = null;
            await last.DisposeAsync();
        }
    }
}
