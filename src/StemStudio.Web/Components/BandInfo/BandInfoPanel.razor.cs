using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.BandInfo;

/// <summary>
/// Loads the band info of a track, polls it while the lookup is scheduled or running (backend: the first open of an old
/// track moves it to pending), and offers retry and correction (FR-009). Raises <see cref="OnChanged"/> when a lookup settles,
/// so the host can refresh the track (the cover may have changed).
/// </summary>
public sealed partial class BandInfoPanel : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private PollLoop? _loop;
    private ElementReference _heading;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    /// <summary>The track (its names prefill the correction dialog).</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>Close button or Escape.</summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    /// <summary>Raised with the settled band info after a lookup finished while the panel was open.</summary>
    [Parameter]
    public EventCallback<BandInfoDto> OnChanged { get; set; }

    private BandInfoDto? Info { get; set; }

    private string? Error { get; set; }

    private bool Busy { get; set; }

    private bool Correcting { get; set; }

    private string? CorrectError { get; set; }

    private bool CanRetry => Info?.Status is MetadataStatusDto.Failed or MetadataStatusDto.NotFound;

    private bool CanCorrect => Info?.Status is MetadataStatusDto.Found or MetadataStatusDto.NotFound or MetadataStatusDto.Failed;

    private bool NeedsPolling => Info?.Status is MetadataStatusDto.None or MetadataStatusDto.Pending or MetadataStatusDto.Searching;

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        await LoadAsync();
        StartPolling();
    }

    /// <inheritdoc />
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await _heading.FocusAsync();
        }
    }

    private async Task LoadAsync()
    {
        try
        {
            Info = await Api.GetBandInfoAsync(Track.Id, _stop.Token);
            Error = null;
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
    }

    private void StartPolling() => (_loop ??= new PollLoop(Timer)).Start(() => NeedsPolling, PollOnceAsync, SettledAsync, _stop.Token);

    private async Task PollOnceAsync(CancellationToken ct)
    {
        await LoadAsync();
        await InvokeAsync(StateHasChanged);
    }

    private Task SettledAsync() => InvokeAsync(() => OnChanged.InvokeAsync(Info!));

    private Task OnKeyAsync(KeyboardEventArgs args) => args.Key == "Escape" ? OnClose.InvokeAsync() : Task.CompletedTask;

    private async Task RetryAsync() => Error = await LookupAsync(null);

    private void OpenCorrection() => (Correcting, CorrectError) = (true, null);

    private void CloseCorrection() => Correcting = false;

    private async Task CorrectAsync(MetadataLookupRequest request)
    {
        CorrectError = await LookupAsync(request);
        Correcting = CorrectError is not null;
    }

    private async Task<string?> LookupAsync(MetadataLookupRequest? request)
    {
        Busy = true;
        var problem = await SendLookupAsync(request);
        Busy = false;
        if (problem is null)
        {
            await LoadAsync();
            StartPolling();
        }

        return problem;
    }

    private async Task<string?> SendLookupAsync(MetadataLookupRequest? request)
    {
        try
        {
            await Api.LookupMetadataAsync(Track.Id, request, _stop.Token);
            return null;
        }
        catch (ApiException ex)
        {
            return ex.Error.UserMessage;
        }
    }
}
