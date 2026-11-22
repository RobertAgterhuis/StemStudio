using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Scores;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.InstrumentViews;

/// <summary>Reuses or creates a sheet music item of one channel and downloads its MIDI (016 FR-023, at sounding pitch).</summary>
public sealed partial class MidiAction : IDisposable
{
    private readonly CancellationTokenSource _stop = new();

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    [Inject]
    private IOptions<StemStudioWebOptions> Options { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    /// <summary>The track of the channel.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>The channel id, <c>bass</c> or <c>drums.kick</c>.</summary>
    [Parameter, EditorRequired]
    public string Channel { get; set; } = string.Empty;

    private bool Busy { get; set; }

    private int Progress { get; set; }

    private string? Error { get; set; }

    private string Key => Channel.Replace('.', '-');

    private string Label => Channel[(Channel.LastIndexOf('.') + 1)..];

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    // The click handler returns at once (no awaiting of the job in a click handler); the work goes on in the background.
    private Task StartAsync()
    {
        if (!Busy)
        {
            Busy = true;
            Progress = 0;
            Error = null;
            _ = RunAsync();
        }

        return Task.CompletedTask;
    }

    private async Task RunAsync()
    {
        await GetAndDownloadAsync();
        Busy = false;
        if (!_stop.IsCancellationRequested)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task GetAndDownloadAsync()
    {
        try
        {
            var item = await ReadyItemAsync() ?? await CreateAsync();
            if (item is not null)
            {
                Download(item);
            }
        }
        catch (ApiException ex)
        {
            Error = ex.Error.UserMessage;
        }
        catch (OperationCanceledException)
        {
            // The component was removed while it waited.
        }
    }

    private async Task<AssetDto?> ReadyItemAsync()
    {
        var list = await Api.ListChannelScoresAsync(Track.Id, Channel, _stop.Token);
        return list.Items.FirstOrDefault(a => a is { Kind: AssetKindDto.Score, Status: AssetStatusDto.Ready });
    }

    private async Task<AssetDto?> CreateAsync()
    {
        var target = ScoreTarget.ForChannel(Track, Channel);
        var form = new ScoreForm(await Api.GetScoreOptionsAsync(target.Source, _stop.Token), target);
        if (form.Tab is { } tab)
        {
            tab.Layout = TabLayoutChoice.None;
        }

        Error = form.Validate();
        return Error is null ? await WaitAsync(await Api.CreateScoreAsync(form.Request(), _stop.Token)) : null;
    }

    private async Task<AssetDto?> WaitAsync(AssetDto item)
    {
        while (item.Status is AssetStatusDto.Queued or AssetStatusDto.Rendering)
        {
            if (!await Timer.WaitAsync(_stop.Token))
            {
                return null;
            }

            item = await RefreshAsync(item.Id);
        }

        return item.Status == AssetStatusDto.Failed ? Failed(item) : item;
    }

    private async Task<AssetDto> RefreshAsync(Guid id)
    {
        var item = await Api.GetAssetAsync(id, _stop.Token);
        Progress = item.Progress;
        await InvokeAsync(StateHasChanged);
        return item;
    }

    private AssetDto? Failed(AssetDto item)
    {
        Error = item.Error ?? "The MIDI file could not be made.";
        return null;
    }

    private void Download(AssetDto item) =>
        Navigation.NavigateTo(new Uri(Options.Value.PublicApiBaseUrl, ApiRoutes.AssetFile(item.Id, ApiRoutes.AssetFileMidi, false)).ToString(), forceLoad: true);
}
