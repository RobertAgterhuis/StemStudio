using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>
/// Track detail (<c>/tracks/{id}</c>): polls progress about once per second until the track is terminal and its
/// detail phase is not queued or running (FR-008, US1-4, US1-8; 002 FR-007).
/// </summary>
public sealed partial class TrackDetail
{
    private IDisposable? _openMixer;

    [Inject]
    private IStemStudioApiClient Api { get; set; } = default!;

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

    /// <summary>True while the standard separation, the detail phase or the online lookup is still in progress.</summary>
    protected override bool NeedsPolling =>
        !TrackStatusText.IsTerminal(Track!.Status) || DetailPhaseText.IsActive(Track.Detail.Status) || MetadataStatusText.IsActive(Track.Metadata.Status);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        _openMixer?.Dispose();
        base.Dispose(disposing);
    }

    /// <inheritdoc />
    protected override async Task OnInitializedAsync()
    {
        try
        {
            Track = await Api.GetTrackAsync(Id, Stopping);
            StartPolling();
            await LoadGuitaristAsync();
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

    /// <inheritdoc />
    protected override async Task RefreshAsync(CancellationToken ct)
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
