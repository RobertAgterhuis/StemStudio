using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>Track detail (<c>/tracks/{id}</c>): polls progress about once per second until terminal (FR-008, US1-4, US1-8).</summary>
public sealed partial class TrackDetail : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _polling;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    /// <summary>The track id from the route.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The latest track state.</summary>
    private TrackDto? Track { get; set; }

    /// <summary>Error when the track cannot be loaded at all (e.g. 404).</summary>
    private string? LoadError { get; set; }

    /// <summary>Error of the last poll, retry or delete, cleared by the next successful poll or retry.</summary>
    private string? ActionError { get; set; }

    /// <inheritdoc />
    public void Dispose()
    {
        _stop.Cancel();
        _stop.Dispose();
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Track = await Api.GetTrackAsync(Id, _stop.Token);
            StartPolling();
        }
        catch (ApiException ex)
        {
            LoadError = ex.Error.UserMessage;
        }
    }

    private void StartPolling()
    {
        if (!TrackStatusText.IsTerminal(Track!.Status) && _polling is not { IsCompleted: false })
        {
            _polling = PollAsync(_stop.Token);
        }
    }

    private async Task PollAsync(CancellationToken ct)
    {
        while (await Timer.WaitAsync(ct))
        {
            await RefreshAsync(ct);
            await InvokeAsync(StateHasChanged);
            if (TrackStatusText.IsTerminal(Track!.Status))
            {
                return;
            }
        }
    }

    private async Task RefreshAsync(CancellationToken ct)
    {
        try
        {
            Track = await Api.GetTrackAsync(Id, ct);
            ActionError = null;
        }
        catch (ApiException ex)
        {
            ActionError = ex.Error.UserMessage;
        }
    }
}
