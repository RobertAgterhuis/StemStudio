using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// The drum trainer (016 US3): loads the drum hits (detecting them on the first open of an old track, polling while
/// queued or running, and after "Split the drums"), attaches the engine-owned hit lanes once the track is loaded, offers
/// the guide piece and the look-ahead at Advanced, and lists the hits of the current bar as text (FR-062).
/// </summary>
public sealed partial class DrumTrainer : IAsyncDisposable
{
    /// <summary>Element id of the engine-owned lane host.</summary>
    public const string HostId = "drum-lanes-host";

    private const int DefaultLookAhead = 4000;
    private const int MinLookAhead = 2000;
    private const int MaxLookAhead = 8000;

    // Lane order top to bottom: drum notation order (data-model 5.2).
    private static readonly DrumPieceDto[] LaneOrder = [DrumPieceDto.Cymbals, DrumPieceDto.Hihat, DrumPieceDto.Toms, DrumPieceDto.Snare, DrumPieceDto.Kick];

    private readonly CancellationTokenSource _stop = new();
    private IReadOnlyList<DrumHitEvent>? _attached;
    private PollLoop? _poll;

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    [Inject]
    private IInstrumentViewEngine Views { get; set; } = default!;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    /// <summary>The track of the mixer.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>True once the track is loaded into the engine; the lanes are attached only then.</summary>
    [Parameter]
    public bool EngineReady { get; set; }

    /// <summary>Reduced motion: the lanes page instead of scrolling.</summary>
    [Parameter]
    public bool ReducedMotion { get; set; }

    /// <summary>The play position in seconds (for the bar text).</summary>
    [Parameter]
    public double Position { get; set; }

    /// <summary>The current guide piece, or null.</summary>
    [Parameter]
    public DrumPieceDto? GuidePiece { get; set; }

    /// <summary>The pieces with a drum detail channel (possible guides).</summary>
    [Parameter]
    public IReadOnlyList<DrumPieceDto> GuidePieces { get; set; } = [];

    /// <summary>Raised with the new guide piece, or null to release it.</summary>
    [Parameter]
    public EventCallback<DrumPieceDto?> OnGuide { get; set; }

    private TrackAnalysisDto<DrumHitsDocumentDto>? Analysis { get; set; }

    private IReadOnlyList<DrumHitEvent>? Events { get; set; }

    private string? Error { get; set; }

    private bool Splitting { get; set; }

    private int LookAheadMs { get; set; } = DefaultLookAhead;

    private bool Advanced => (Experience ?? ExperienceContext.Default).IsVisible("drums.guide");

    private string WireStatus => Analysis is null ? "none" : WireName.Of(Analysis.Status);

    private string StatusText => AnalysisText.Status(Analysis, "Drum hit detection", "drum hits");

    private bool SplitRequired => Analysis?.Status == AnalysisStatusDto.Unavailable;

    private string SplitText => Splitting
        ? "Splitting the drums into kick, snare, toms, hi-hat and cymbals. The hits are found right after."
        : "The drum trainer needs the drums split into their pieces first. Drumless works without it.";

    private string LookAheadText => string.Create(CultureInfo.InvariantCulture, $"{LookAheadMs / 1000.0:0.#} s");

    private DrumBar Bar => DrumBarText.At(Analysis?.Document?.Hits ?? [], Track.BeatGrid, Position);

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await _stop.CancelAsync();
        try
        {
            await (_attached is null ? Task.CompletedTask : Views.DetachHitLanesAsync());
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
        LookAheadMs = Advanced ? Math.Clamp(await Preferences.GetAsync<int?>(ViewPreferences.LookAheadKey) ?? DefaultLookAhead, MinLookAhead, MaxLookAhead) : DefaultLookAhead;
        await LoadAsync(() => Api.GetDrumHitsAsync(Track.Id, _stop.Token));
        await (Analysis?.Status == AnalysisStatusDto.None ? LoadAsync(() => Api.DetectDrumHitsAsync(Track.Id, _stop.Token)) : Task.CompletedTask);
        StartPolling();
        await LoadScoresAsync();
    }

    /// <inheritdoc />
    protected override Task OnAfterRenderAsync(bool firstRender) => SyncLanesAsync();

    private bool CanGuide(DrumPieceDto piece) => Advanced && GuidePieces.Contains(piece);

    private string? Pressed(DrumPieceDto piece) => Advanced ? (GuidePiece == piece ? "true" : "false") : null;

    private Task ToggleGuideAsync(DrumPieceDto piece) => OnGuide.InvokeAsync(GuidePiece == piece ? null : piece);

    private void StartPolling() =>
        (_poll ??= new PollLoop(Timer)).Start(() => AnalysisText.InProgress(Analysis) || (Splitting && SplitRequired), PollAsync, () => Task.CompletedTask, _stop.Token);

    private async Task PollAsync(CancellationToken ct)
    {
        await LoadAsync(() => Api.GetDrumHitsAsync(Track.Id, ct));
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadAsync(Func<Task<TrackAnalysisDto<DrumHitsDocumentDto>>> request)
    {
        try
        {
            Analysis = await request();
            Events = Analysis is { Status: AnalysisStatusDto.Ready, Document: { } document } ? ToEngine(document) : null;
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private static DrumHitEvent[] ToEngine(DrumHitsDocumentDto document) =>
        [.. document.Hits.Select(h => new DrumHitEvent(h.TimeMs, WireName.Of(h.Piece), h.Strength))];

    private async Task StartSplitAsync()
    {
        Splitting = true;
        try
        {
            await Api.StartDetailAsync(Track.Id, _stop.Token);
            StartPolling();
        }
        catch (ApiException ex)
        {
            (Splitting, Error) = (false, ex.Error.UserMessage);
        }
    }

    private async Task SetLookAheadAsync(ChangeEventArgs args)
    {
        LookAheadMs = Math.Clamp(int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var ms) ? ms : DefaultLookAhead, MinLookAhead, MaxLookAhead);
        await Preferences.SetAsync(ViewPreferences.LookAheadKey, LookAheadMs);
        await EngineAsync(() => _attached is null ? Task.CompletedTask : Views.SetHitLaneOptionsAsync(new HitLaneOptions { LookAheadMs = LookAheadMs }));
    }

    /// <summary>Attaches the lanes when they should be drawn and differ from the attached hits; detaches them when they should not.</summary>
    private async Task SyncLanesAsync()
    {
        var wanted = EngineReady ? Events : null;
        if (!ReferenceEquals(wanted, _attached))
        {
            _attached = wanted;
            await ShowLanesAsync(wanted);
        }
    }

    private async Task ShowLanesAsync(IReadOnlyList<DrumHitEvent>? hits)
    {
        var options = new HitLaneOptions { LookAheadMs = LookAheadMs, ReducedMotion = ReducedMotion ? true : null };
        if (!await EngineAsync(() => hits is null ? Views.DetachHitLanesAsync() : Views.AttachHitLanesAsync(HostId, hits, options)))
        {
            StateHasChanged();
        }
    }

    private async Task<bool> EngineAsync(Func<Task> call)
    {
        try
        {
            await call();
            return true;
        }
        catch (AudioEngineException ex)
        {
            Error = $"The drum lanes could not be shown: {ex.Message}";
            return false;
        }
    }
}
