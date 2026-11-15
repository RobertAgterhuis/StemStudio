using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Services;

namespace StemStudio.Web.Components.Pages;

/// <summary>Detail phase section of the track detail page; shown for completed tracks only.</summary>
public sealed partial class DetailSection
{
    /// <summary>The completed track whose detail phase is shown.</summary>
    [Parameter, EditorRequired]
    public TrackDto Track { get; set; } = default!;

    /// <summary>True while a start or cancel request runs (buttons busy and disabled).</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>Raised by Retry (failed, cancelled): queue the detail phase again.</summary>
    [Parameter]
    public EventCallback OnStart { get; set; }

    /// <summary>Raised by Cancel (queued, running).</summary>
    [Parameter]
    public EventCallback OnCancel { get; set; }

    private DetailPhaseDto Detail => Track.Detail;

    private bool CanRetry => Detail.Status is DetailPhaseStatusDto.Failed or DetailPhaseStatusDto.Cancelled;

    private bool Active => DetailPhaseText.IsActive(Detail.Status);

    private string? Notice => DetailPhaseText.Notice(Track);
}
