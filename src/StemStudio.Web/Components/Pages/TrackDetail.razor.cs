using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Track detail (<c>/tracks/{id}</c>): polls progress about once per second until the track is terminal and its
/// detail phase is not queued or running (FR-008, US1-4, US1-8; 002 FR-007).
/// </summary>
public sealed partial class TrackDetail : IDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _polling;
    private IDisposable? _openMixer;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

    [Inject]
    private IPollingTimer Timer { get; set; } = default!;

    [Inject]
    private PageCommands Commands { get; set; } = default!;

    /// <summary>The track id from the route.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The latest track state.</summary>
    private TrackDto? Track { get; set; }

    /// <summary>Error when the track cannot be loaded at all (e.g. 404).</summary>
    private string? LoadError { get; set; }

    /// <summary>Error of the last poll, retry or delete, cleared by the next successful poll or retry.</summary>
    private string? ActionError { get; set; }

    /// <summary>The stem names of the track for the ribbon.</summary>
    private IReadOnlyList<string> StemList => Track!.Stems.Select(s => s.Name).ToList();

    /// <summary>True while the standard separation or the detail phase is still in progress.</summary>
    private bool NeedsPolling => !TrackStatusText.IsTerminal(Track!.Status) || DetailPhaseText.IsActive(Track.Detail.Status);

    /// <inheritdoc />
    public void Dispose()
    {
        _openMixer?.Dispose();
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

    /// <inheritdoc />
    protected override void OnAfterRender(bool firstRender)
    {
        if (_openMixer is null && Track?.Status == TrackStatusDto.Completed)
        {
            _openMixer = Commands.Register("open-mixer", "Open mixer", () =>
            {
                Navigation.NavigateTo($"tracks/{Id}/mixer");
                return Task.CompletedTask;
            });
        }
    }

    private void StartPolling()
    {
        if (NeedsPolling && _polling is not { IsCompleted: false })
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
            if (!NeedsPolling)
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
