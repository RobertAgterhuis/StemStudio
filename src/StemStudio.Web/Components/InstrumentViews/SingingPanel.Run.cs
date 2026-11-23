using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts.Recording;
using StemStudio.Web.Audio;
using StemStudio.Web.InstrumentViews;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// Running singing feedback (FR-041..FR-045): start and stop through the engine, the lead vocal muted by the mixer while it
/// runs (<see cref="OnRunningChanged"/>), unit scores announced and reported to the banner, the summary on stop or failure.
/// </summary>
public sealed partial class SingingPanel : IAsyncDisposable
{
    private const string NoInput = "No input device is open";
    private string _kind = "Line";

    [Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    /// <summary>True once the track is loaded into the engine.</summary>
    [Parameter]
    public bool EngineReady { get; set; }

    /// <summary>Reduced motion: the pitch view pages instead of scrolling.</summary>
    [Parameter]
    public bool ReducedMotion { get; set; }

    /// <summary>Raised with true before the feedback starts and with false when it ended (the mixer mutes the lead vocal).</summary>
    [Parameter]
    public EventCallback<bool> OnRunningChanged { get; set; }

    /// <summary>Raised with every unit score, and with null when a new run starts (the banner shows the last one).</summary>
    [Parameter]
    public EventCallback<ScoredUnit?> OnUnitScored { get; set; }

    /// <summary>Raised by the link to the 015 input settings in an error.</summary>
    [Parameter]
    public EventCallback OnOpenInputSettings { get; set; }

    /// <summary>The mixer's playback state; the end of the song (playing -> stopped) ends the feedback like a stop (FR-046).</summary>
    [Parameter]
    public PlaybackState Playback { get; set; }

    private bool Running { get; set; }

    private string StateName { get; set; } = "idle";

    private string? Error { get; set; }

    private string? Announcement { get; set; }

    private SingingSummary? Summary { get; set; }

    private string ToggleText => Running ? "Stop singing feedback" : "Start singing feedback";

    private string StateText => StateName switch
    {
        "starting" => "Starting the microphone...",
        "running" => "Listening: sing along with the song.",
        "stopped" => "Stopped.",
        "failed" => "Stopped because of a problem.",
        _ => "Not listening.",
    };

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        Views.SingingUnitScored -= OnScored;
        Views.SingingStateChanged -= OnStateChanged;
        await _stop.CancelAsync();
        try
        {
            await (Running ? EndAsync() : Task.CompletedTask);
        }
        catch (Exception ex) when (ex is InvalidOperationException or AudioEngineException or JSDisconnectedException)
        {
            // The engine or the circuit is gone.
        }

        _stop.Dispose();
    }

    private void Subscribe()
    {
        Views.SingingUnitScored += OnScored;
        Views.SingingStateChanged += OnStateChanged;
    }

    private Task ToggleAsync() => Running ? EndAsync() : StartAsync();

    private async Task StartAsync()
    {
        var units = SingingUnits.From(Lyrics, Sections, Track.BeatGrid, Duration);
        (_kind, Running, StateName, Error, Summary, Announcement) = (units.Kind, true, "starting", null, null, null);
        await OnRunningChanged.InvokeAsync(true);
        await OnUnitScored.InvokeAsync(null);
        if (!await EngineAsync(() => Views.StartSingingFeedbackAsync(Request(units))))
        {
            (Running, StateName) = (false, "failed");
            await OnRunningChanged.InvokeAsync(false);
        }
    }

    private SingingFeedbackRequest Request(UnitSet units)
    {
        var calibrated = Latency is { Method: not LatencyMethodDto.Estimated } latency ? latency : null;
        return new SingingFeedbackRequest
        {
            PitchViewElementId = HostId,
            Reference = SingingUnits.Reference(Analysis!.Document!),
            Units = units.Units,
            ToleranceCents = Tolerance,
            RoundTripMs = calibrated is null ? null : Math.Round(calibrated.Samples * 1000.0 / RecordingLimits.SampleRate, 1),
            LatencyMethod = calibrated?.MethodName,
            ReducedMotion = ReducedMotion ? true : null,
        };
    }

    // Stops the engine and shows the summary (idempotent in the engine), then lets the mixer restore the lead vocal.
    private async Task EndAsync()
    {
        Running = false;
        await EngineAsync(async () => Summary = await Views.StopSingingFeedbackAsync());
        await OnRunningChanged.InvokeAsync(false);
    }

    private void OnScored(object? sender, SingingUnitScore score) => _ = InvokeAsync(async () =>
    {
        Announcement = SingingUnits.Announce(_kind, score);
        await OnUnitScored.InvokeAsync(new ScoredUnit(_kind, score));
        StateHasChanged();
    });

    private void OnStateChanged(object? sender, SingingStateChange change) => _ = InvokeAsync(async () =>
    {
        StateName = change.State;
        Error = change.State == "failed" ? change.Reason ?? "Singing feedback stopped." : Error;
        await (change.State is "failed" or "stopped" && Running ? EndAsync() : Task.CompletedTask);
        StateHasChanged();
    });

    private Task OpenInputSettingsAsync() => OnOpenInputSettings.InvokeAsync();

    private async Task<bool> EngineAsync(Func<Task> call)
    {
        try
        {
            await call();
            return true;
        }
        catch (AudioEngineException ex)
        {
            Error = ex.Message == NoInput ? "No microphone is open. Choose your microphone in the recording input settings, then start again." : ex.Message;
            return false;
        }
    }

    private string SummaryText(SingingSummary summary)
    {
        if (summary.UnitsScored == 0 || summary.Average is not { } average)
        {
            return "No line was scored: sing along while the song plays.";
        }

        var kind = _kind.ToLowerInvariant();
        var count = summary.UnitsScored == 1 ? kind : $"{kind}s";
        var best = summary.Best is { } b ? string.Create(CultureInfo.InvariantCulture, $" Best: {kind} {b.Index + 1} ({b.Score}).") : string.Empty;
        return string.Create(CultureInfo.InvariantCulture, $"Average {average} over {summary.UnitsScored} {count}.{best}");
    }
}
