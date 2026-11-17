using StemStudio.Contracts.Recording;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Services;

namespace StemStudio.Web.Recording;

/// <summary>The services a <see cref="RecordingSession"/> works with (scoped per circuit).</summary>
/// <param name="Engine">The recording engine.</param>
/// <param name="Audio">The engine wrapper.</param>
/// <param name="Api">The backend client.</param>
/// <param name="Preferences">The stored recording choices.</param>
/// <param name="Timer">Polls saving takes (about 1 s).</param>
public sealed record RecordingServices(
    IRecordingEngine Engine,
    IAudioEngine Audio,
    IStemStudioApiClient Api,
    RecordingPreferences Preferences,
    IPollingTimer Timer);

/// <summary>
/// The recording UI of one mixer page (spec 015 US1): panel open/closed, the input, Record/Stop with its block reasons,
/// the live state from the engine callbacks, uploads and the take strip. Components only render it and call its methods.
/// </summary>
public sealed partial class RecordingSession : IAsyncDisposable
{
    private readonly RecordingServices _services;
    private readonly IRecordingHost _host;
    private readonly TakeUploader _uploader;
    private readonly HashSet<string> _handled = [];
    private readonly CancellationTokenSource _stop = new();
    private List<TakeItem> _lastSession = [];
    private RecordStart? _start;
    private bool _polling;

    /// <summary>Creates the session and subscribes to the engine callbacks.</summary>
    /// <param name="services">The services.</param>
    /// <param name="host">The mixer page.</param>
    /// <param name="audioUrl">Makes a relative take audio URL absolute.</param>
    public RecordingSession(RecordingServices services, IRecordingHost host, Func<string, string> audioUrl)
    {
        _services = services;
        _host = host;
        _uploader = new TakeUploader(services.Engine, services.Api);
        Input = new InputSetup(services.Engine, services.Audio, services.Preferences);
        Latency = new LatencyChoice(services.Api);
        Takes = new TakeLibrary(services.Engine, services.Audio, services.Api, audioUrl);
        Subscribe(services.Engine.Callbacks);
    }

    /// <summary>Permission, devices and choices.</summary>
    public InputSetup Input { get; }

    /// <summary>Calibrations and the latency in use.</summary>
    public LatencyChoice Latency { get; }

    /// <summary>Takes and the take strip.</summary>
    public TakeLibrary Takes { get; }

    /// <summary>True while the recording panel is shown.</summary>
    public bool PanelOpen { get; private set; }

    /// <summary>The engine's recording state.</summary>
    public RecordingState State { get; private set; } = RecordingState.Closed;

    /// <summary>The last progress report, or null.</summary>
    public RecordingProgress? Progress { get; private set; }

    /// <summary>A readable error of the last Record/Stop, or null.</summary>
    public string? Error { get; private set; }

    /// <summary>True from Record until the engine reports the count-in or recording state.</summary>
    public bool Starting { get; private set; }

    /// <summary>True while counting in, recording or finishing.</summary>
    public bool IsRecording => State is RecordingState.CountIn or RecordingState.Recording or RecordingState.Finishing;

    /// <summary>Why Record is disabled, or null (never while recording, so Stop always works).</summary>
    public RecordBlock? Block => IsRecording ? null : Blocks.FirstOrDefault(b => b is not null);

    /// <summary>The latency for the open input.</summary>
    public LatencyInUse? LatencyInUse => Latency.InUse(Input.Input);

    /// <summary>Opens the panel (support, permission, takes, calibrations) or closes it and releases the microphone.</summary>
    /// <returns>A task that completes when the panel state is known.</returns>
    public async Task TogglePanelAsync()
    {
        PanelOpen = !PanelOpen;
        await (PanelOpen ? OpenPanelAsync() : ClosePanelAsync());
    }

    /// <summary>Record when idle, Stop while recording (button and <c>R</c>).</summary>
    /// <returns>A task that completes when started or stopped.</returns>
    public Task ToggleRecordAsync() => IsRecording ? StopAsync() : Block is null && !Starting ? StartAsync() : Task.CompletedTask;

    /// <summary>"Save again": uploads every refused or failed local capture once more.</summary>
    /// <returns>A task that completes when tried.</returns>
    public Task SaveAgainAsync() => UploadAsync([.. Takes.Items.Where(i => i.UploadFailed)]);

    /// <summary>Re-renders the mixer (after a component changed the session).</summary>
    /// <returns>A task that completes when queued.</returns>
    public Task RefreshAsync() => _host.RefreshAsync();

    /// <summary>"Set to 100 %" (FR-009).</summary>
    /// <returns>A task that completes when applied.</returns>
    public Task SetNormalSpeedAsync() => _host.SetNormalSpeedAsync();

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Unsubscribe(_services.Engine.Callbacks);
        await _stop.CancelAsync();
        _stop.Dispose();
        _uploader.Dispose();
        await (IsRecording ? Task.CompletedTask : Input.CloseAsync());
    }

    private IEnumerable<RecordBlock?> Blocks =>
    [
        _host.TrackReady ? null : new("track", "The track is still loading."),
        Input.IsOpen ? null : new("input", "Open an input above to record."),
        _host.Practice is { SpeedPercent: 100, TransposeSemitones: 0 } ? null : new("speed", RecordingText.SpeedRule),
        _host.Practice.Trainer.Enabled ? new("trainer", "Switch the speed trainer off to record.") : null,
        Takes.Count < RecordingLimits.MaxTakesPerTrack ? null : new("limit", "This track has 200 takes. Delete some to record more."),
    ];

    private async Task OpenPanelAsync()
    {
        await Input.LoadAsync();
        await Latency.LoadAsync();
        await Takes.LoadAsync(_host.TrackId);
    }

    private async Task ClosePanelAsync()
    {
        await (IsRecording ? StopAsync() : Task.CompletedTask);
        await Input.CloseAsync();
    }

    private async Task StartAsync()
    {
        var settings = Input.Settings;
        var latency = LatencyInUse!;
        (Starting, Error, _start) = (true, null, new RecordStart(settings.Part, latency.CalibrationId, Input.Input!.Label));
        try
        {
            await _services.Engine.StartRecordingAsync(Options(settings, latency));
        }
        catch (RecordingException ex)
        {
            Error = ex.Message;
        }
        finally
        {
            Starting = false;
        }
    }

    private static RecordOptions Options(RecordingSettings settings, LatencyInUse latency) => new()
    {
        LatencySamples = latency.Samples,
        LatencyMethod = latency.Method,
        Part = settings.Part,
        MuteOriginal = settings.MuteOriginal && settings.Part != RecordingPartDto.None,
        CountInBars = settings.CountInBars,
    };

    private async Task StopAsync()
    {
        try
        {
            await CapturedAsync(await _services.Engine.StopRecordingAsync());
        }
        catch (RecordingException ex)
        {
            Error = ex.Message;
        }
    }

    private sealed record RecordStart(RecordingPartDto Part, Guid? CalibrationId, string DeviceLabel);
}
