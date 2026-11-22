using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// The chords of the mixer (016 US1): loads the chord analysis (polled while queued or running; the backend detects
/// chords on its own after the separation, FR-010), offers detection, re-detection with the overwrite question (FR-012),
/// and attaches the engine-owned chord lane once the track is loaded. Every loaded analysis is reported through
/// <see cref="OnLoaded"/>, so the mixer can put the chords on the karaoke banner and show the key.
/// </summary>
public sealed partial class ChordPanel : IAsyncDisposable
{
    /// <summary>Element id of the engine-owned lane host.</summary>
    public const string HostId = "chord-lane-host";

    private readonly CancellationTokenSource _stop = new();
    private PollLoop? _poll;
    private ChordChartData? _attached;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    [Inject]
    private IConfirmService Confirm { get; set; } = default!;

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    /// <summary>The track of the mixer.</summary>
    [Parameter, EditorRequired]
    public Guid TrackId { get; set; }

    /// <summary>True once the track is loaded into the engine; the lane is attached only then.</summary>
    [Parameter]
    public bool EngineReady { get; set; }

    /// <summary>Reduced motion: the lane pages instead of scrolling.</summary>
    [Parameter]
    public bool ReducedMotion { get; set; }

    /// <summary>Raised with every loaded analysis.</summary>
    [Parameter]
    public EventCallback<TrackAnalysisDto<ChordChartDocumentDto>> OnLoaded { get; set; }

    private TrackAnalysisDto<ChordChartDocumentDto>? Analysis { get; set; }

    private ChordChartData? Chart { get; set; }

    private bool LaneVisible { get; set; } = true;

    private bool Busy { get; set; }

    private string? Error { get; set; }

    private ChordVocabularyDto Vocabulary { get; set; } = ChordVocabularyDto.Simple;

    private ChordEngineDto Engine { get; set; } = ChordEngineDto.Btc;

    private string WireStatus => Analysis is null ? "none" : WireName.Of(Analysis.Status);

    private string StatusText => AnalysisText.Status(Analysis, "Chord detection", "chords");

    private bool CanDetect => Analysis?.Status is AnalysisStatusDto.None or AnalysisStatusDto.Failed or AnalysisStatusDto.Stale;

    private bool CanRedetect => Analysis?.Status == AnalysisStatusDto.Ready;

    private string DetectText => Analysis?.Status == AnalysisStatusDto.Failed ? "Try again" : "Detect chords";

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await (_attached is null ? Task.CompletedTask : Views.DetachChordLaneAsync());
        }
        catch (Exception ex) when (ex is InvalidOperationException or AudioEngineException or JSDisconnectedException)
        {
            // The engine or the circuit is gone; nothing left to detach.
        }

        _stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        LaneVisible = await Preferences.GetAsync<bool?>(ViewPreferences.LaneKey) ?? true;
        await LoadAsync(() => Api.GetChordsAsync(TrackId, _stop.Token));
        StartPolling();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => SyncLaneAsync();

    private void StartPolling() =>
        (_poll ??= new PollLoop(Timer)).Start(() => AnalysisText.InProgress(Analysis), PollAsync, () => Task.CompletedTask, _stop.Token);

    private async Task PollAsync(CancellationToken ct)
    {
        await LoadAsync(() => Api.GetChordsAsync(TrackId, ct));
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadAsync(Func<Task<TrackAnalysisDto<ChordChartDocumentDto>>> request, bool rethrowEditsExist = false)
    {
        try
        {
            Apply(await request());
            Error = null;
            await OnLoaded.InvokeAsync(Analysis!);
        }
        catch (ApiException ex) when (!(rethrowEditsExist && ex.Error.Code == "edits-exist"))
        {
            Error = ex.Error.UserMessage;
        }
    }

    private void Apply(TrackAnalysisDto<ChordChartDocumentDto> analysis)
    {
        Analysis = analysis;
        var document = analysis.Status == AnalysisStatusDto.Ready ? analysis.Document : null;
        Chart = document is { NoChords: false } ? ChordLabels.ToEngine(document) : null;
        Vocabulary = analysis.Document?.Vocabulary ?? Vocabulary;
    }

    private async Task ToggleLaneAsync()
    {
        LaneVisible = !LaneVisible;
        await Preferences.SetAsync(ViewPreferences.LaneKey, LaneVisible);
    }

    private async Task DetectAsync()
    {
        var overwrite = Analysis?.Document?.HasEdits == true;
        if (overwrite && !await ConfirmOverwriteAsync())
        {
            return;
        }

        Busy = true;
        await DetectOrAskAsync(overwrite);
        Busy = false;
        StartPolling();
    }

    // The backend refuses when edits exist (409 edits-exist, FR-012): ask, then detect again with the overwrite.
    private async Task DetectOrAskAsync(bool overwrite)
    {
        if (!await TryDetectAsync(overwrite) && await ConfirmOverwriteAsync())
        {
            await TryDetectAsync(overwrite: true);
        }
    }

    // False when the backend refuses because edits exist; every other API error is shown by LoadAsync.
    private async Task<bool> TryDetectAsync(bool overwrite)
    {
        try
        {
            await LoadAsync(() => Api.DetectChordsAsync(TrackId, Request(overwrite), _stop.Token), rethrowEditsExist: true);
            return true;
        }
        catch (ApiException ex) when (ex.Error.Code == "edits-exist")
        {
            return false;
        }
    }

    private DetectChordsRequest Request(bool overwrite) => new()
    {
        Vocabulary = Vocabulary,
        Engine = Engine == ChordEngineDto.Btc ? null : Engine,
        OverwriteEdits = overwrite,
    };

    private Task<bool> ConfirmOverwriteAsync() => Confirm.ConfirmAsync(
        "Replace your chord edits?", "Detecting again replaces the chords you corrected with newly detected ones.", "Replace", "chords-overwrite-confirm");

    /// <summary>Attaches the lane when it should be drawn and differs from the attached chart; detaches it when it should not.</summary>
    private async Task SyncLaneAsync()
    {
        var wanted = LaneVisible && EngineReady ? Chart : null;
        if (!ReferenceEquals(wanted, _attached))
        {
            _attached = wanted;
            await ShowLaneAsync(wanted);
        }
    }

    private async Task ShowLaneAsync(ChordChartData? chart)
    {
        try
        {
            await (chart is null ? Views.DetachChordLaneAsync() : Views.AttachChordLaneAsync(HostId, chart, LaneOptions));
        }
        catch (AudioEngineException ex)
        {
            Error = $"The chord lane could not be shown: {ex.Message}";
            StateHasChanged();
        }
    }

    private ChordLaneOptions LaneOptions => new() { ReducedMotion = ReducedMotion ? true : null };
}
