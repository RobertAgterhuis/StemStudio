using System.Globalization;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.InstrumentViews;
using StemStudio.Web.Recording;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>
/// The reference melody of singing feedback (016 US4, FR-040, data-model 7.3): loads it, analyses it on the first open when
/// the estimate is at most 60 s (else offers it with the estimate), polls while it runs; the tolerance and latency
/// texts. Running the feedback lives in <c>SingingPanel.Run.cs</c>.
/// </summary>
public sealed partial class SingingPanel
{
    /// <summary>Element id of the engine-owned pitch view host.</summary>
    public const string HostId = "pitch-view-host";

    private const string StatusId = "singing-reference-status";
    private const int AutoAnalyseSeconds = 60;

    private readonly CancellationTokenSource _stop = new();
    private PollLoop? _poll;
    private bool _gone;

    /// <summary>The track of the mixer.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>The cascaded level.</summary>
    [CascadingParameter]
    public ExperienceContext? Experience { get; set; }

    [Inject]
    private ViewPreferences Preferences { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    /// <summary>Track length in seconds (for the fallback units).</summary>
    [Parameter]
    public double Duration { get; set; }

    /// <summary>The 014 sections, or null.</summary>
    [Parameter]
    public IReadOnlyList<SectionDto>? Sections { get; set; }

    /// <summary>The 015 latency in use, or null without an open input.</summary>
    [Parameter]
    public LatencyInUse? Latency { get; set; }

    private TrackAnalysisDto<VocalPitchDocumentDto>? Analysis { get; set; }

    private LyricsDto? Lyrics { get; set; }

    private bool Busy { get; set; }

    private int Tolerance { get; set; } = 100;

    private ExperienceContext Level => Experience ?? ExperienceContext.Default;

    private bool ReferenceReady => Analysis is { Status: AnalysisStatusDto.Ready, Document: not null };

    private string ReferenceStatus => Analysis is null ? "none" : WireName.Of(Analysis.Status);

    private string ReferenceText => Analysis?.Status switch
    {
        AnalysisStatusDto.None => "The reference melody has not been analysed yet.",
        AnalysisStatusDto.Ready => "The reference melody is ready.",
        _ => AnalysisText.Status(Analysis, "Melody analysis", "reference melody"),
    };

    // A first open analyses at once when it is quick (data-model 7.3); a slow one is offered with its estimate.
    private bool AnalyseAtOnce => Analysis?.Status == AnalysisStatusDto.None && (Analysis.EstimateSeconds ?? 0) <= AutoAnalyseSeconds;

    private bool CanAnalyse => Analysis?.Status is AnalysisStatusDto.None or AnalysisStatusDto.Failed or AnalysisStatusDto.Stale;

    private string AnalyseText => Analysis?.Status == AnalysisStatusDto.Failed ? "Try again" : "Analyse the melody";

    private string? EstimateText => SingingText.Estimate(Analysis?.EstimateSeconds);

    private string LatencySource => Latency?.MethodName ?? "none";

    private string LatencyText => SingingText.Latency(Latency);

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        Subscribe();
        Tolerance = SingingMix.Tolerance(Level, await Preferences.GetAsync<int?>(ViewPreferences.ToleranceKey));
        await LoadAsync(() => Api.GetVocalPitchAsync(Track.Id, _stop.Token));
        await (AnalyseAtOnce ? LoadAsync(() => Api.AnalyseVocalPitchAsync(Track.Id, _stop.Token)) : Task.CompletedTask);
        StartPolling();
        Lyrics = await LyricsAsync();
    }

    private async Task<LyricsDto?> LyricsAsync()
    {
        try
        {
            return await Api.GetLyricsAsync(Track.Id, _stop.Token);
        }
        catch (ApiException)
        {
            return null;
        }
    }

    private void StartPolling() =>
        (_poll ??= new PollLoop(Timer)).Start(() => AnalysisText.KeepPolling(Analysis, _gone), PollAsync, () => Task.CompletedTask, _stop.Token);

    private async Task PollAsync(CancellationToken ct)
    {
        await LoadAsync(() => Api.GetVocalPitchAsync(Track.Id, ct));
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadAsync(Func<Task<TrackAnalysisDto<VocalPitchDocumentDto>>> request)
    {
        try
        {
            Analysis = await request();
        }
        catch (ApiException ex)
        {
            (Error, _gone) = (ex.Error.UserMessage, _gone || ex.Error.IsNotFound);
        }
    }

    private async Task AnalyseAsync()
    {
        Busy = true;
        await LoadAsync(() => Api.AnalyseVocalPitchAsync(Track.Id, _stop.Token));
        Busy = false;
        StartPolling();
    }

    private async Task SetToleranceAsync(ChangeEventArgs args)
    {
        var cents = int.TryParse(args.Value?.ToString(), CultureInfo.InvariantCulture, out var value) ? value : Tolerance;
        Tolerance = Math.Clamp(cents, SingingMix.MinTolerance, SingingMix.MaxTolerance);
        await Preferences.SetAsync(ViewPreferences.ToleranceKey, Tolerance);
        await (Running ? EngineAsync(() => Views.SetSingingToleranceAsync(Tolerance)) : Task.CompletedTask);
    }
}
