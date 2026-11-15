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

    private DetailPhaseDto Detail => Track.Detail;

    private bool Active => DetailPhaseText.IsActive(Detail.Status);

    private string? Notice => DetailPhaseText.Notice(Track);
}
