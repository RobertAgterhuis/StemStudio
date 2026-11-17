using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;

namespace StemStudio.Web.Recording;

/// <summary>Callbacks, uploads and polling of <see cref="RecordingSession"/>.</summary>
public sealed partial class RecordingSession
{
    /// <summary>The live region text (<c>rec-status</c>): "Count-in 3", "Recording", "Saving 2 takes", "Saved".</summary>
    public string? StatusText => State switch
    {
        RecordingState.CountIn => $"Count-in {Progress?.CountInBeatsLeft}".TrimEnd(),
        RecordingState.Recording => "Recording",
        RecordingState.Finishing => "Finishing the take",
        _ => SavingText(),
    };

    /// <summary>Progress of the last session's uploads 0-100, or null when nothing is being saved.</summary>
    public int? SavingPercent => _lastSession.Count == 0 || !_lastSession.Any(i => i.Saving)
        ? null
        : 100 * _lastSession.Count(i => !i.Saving) / _lastSession.Count;

    /// <summary>The first upload error of the local takes, or the last Record/Stop error.</summary>
    public string? UploadError => Takes.Items.FirstOrDefault(i => i.UploadFailed)?.Error;

    /// <summary>True when some local capture can be sent again.</summary>
    public bool CanSaveAgain => Takes.Items.Any(i => i.UploadFailed);

    private string? SavingText()
    {
        var saving = _lastSession.Count(i => i.Saving);
        return saving > 0 ? $"Saving {saving} {(saving == 1 ? "take" : "takes")}"
            : _lastSession.Count > 0 && _lastSession.All(i => i.Take?.Status == TakeStatusDto.Ready) ? "Saved" : null;
    }

    private void Subscribe(RecordingCallbacks callbacks)
    {
        callbacks.StateChanged += OnState;
        callbacks.ProgressChanged += OnProgress;
        callbacks.TakesCaptured += OnTakes;
        callbacks.InputLost += OnLost;
        callbacks.InputDevicesChanged += OnDevices;
    }

    private void Unsubscribe(RecordingCallbacks callbacks)
    {
        callbacks.StateChanged -= OnState;
        callbacks.ProgressChanged -= OnProgress;
        callbacks.TakesCaptured -= OnTakes;
        callbacks.InputLost -= OnLost;
        callbacks.InputDevicesChanged -= OnDevices;
    }

    private void OnState(object? sender, RecordingState state)
    {
        (State, Progress) = (state, state is RecordingState.Open or RecordingState.Closed ? null : Progress);
        _ = _host.RefreshAsync();
    }

    private void OnProgress(object? sender, RecordingProgress progress)
    {
        Progress = progress;
        _ = _host.RefreshAsync();
    }

    private void OnTakes(object? sender, IReadOnlyList<CapturedTake> takes) => _ = RunAsync(() => CapturedAsync(takes));

    private void OnLost(object? sender, string reason)
    {
        Input.MarkLost();
        _ = _host.RefreshAsync();
    }

    private void OnDevices(object? sender, EventArgs e) => _ = RunAsync(Input.RefreshDevicesAsync);

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work();
        }
        catch (Exception ex) when (ex is RecordingException or ApiException or InvalidOperationException)
        {
            Error = ex.Message;
        }

        await _host.RefreshAsync();
    }

    // stopRecording returns the takes and OnTakesCaptured reports them too: each capture key is handled once.
    private async Task CapturedAsync(IReadOnlyList<CapturedTake> takes)
    {
        var fresh = takes.Where(t => _handled.Add(t.CaptureKey)).Select(t => TakeItem.Local(t, Metadata(t))).ToList();
        if (fresh.Count == 0)
        {
            return;
        }

        _lastSession = fresh;
        await Takes.AddAsync(fresh);
        await _host.RefreshAsync();
        await UploadAsync(fresh);
    }

    private async Task UploadAsync(IReadOnlyList<TakeItem> items)
    {
        await _uploader.UploadAsync(_host.TrackId, items);
        await _host.RefreshAsync();
        _ = PollAsync();
    }

    private async Task PollAsync()
    {
        if (_polling)
        {
            return;
        }

        _polling = true;
        try
        {
            while (Takes.SavingOnServer().Count > 0 && await _services.Timer.WaitAsync(_stop.Token))
            {
                await PollOnceAsync();
            }
        }
        finally
        {
            _polling = false;
        }
    }

    private async Task PollOnceAsync()
    {
        foreach (var item in Takes.SavingOnServer())
        {
            var (take, _) = await ApiCall.TryAsync(() => _services.Api.GetTakeAsync(item.Take!.Id));
            await (take is null ? Task.CompletedTask : Takes.ApplyAsync(item, take));
        }

        await _host.RefreshAsync();
    }

    private RecordingTakeUploadDto Metadata(CapturedTake take) => new()
    {
        SessionId = take.SessionId,
        PassIndex = take.PassIndex,
        Partial = take.Partial,
        StartSample = take.StartSample,
        LengthSamples = take.LengthSamples,
        InputMode = take.InputMode,
        LatencySamples = take.LatencySamples,
        LatencyMethod = take.LatencyMethod,
        CalibrationId = _start?.CalibrationId,
        DeviceLabel = _start?.DeviceLabel,
        Part = _start?.Part ?? Input.Settings.Part,
        MixId = _host.MixId,
        Speed = 1.0,
        TransposeSemitones = 0,
        Dropouts = take.Dropouts,
        RecordedAt = take.RecordedAt,
    };
}
