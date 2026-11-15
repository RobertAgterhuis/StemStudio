using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Components.Pages;

/// <summary>Band info panel of the track detail page (feature 005, US2).</summary>
public sealed partial class TrackDetail
{
    /// <summary>True while the band info panel is shown.</summary>
    private bool ShowBandInfo { get; set; }

    /// <summary>The own picture was set or reset.</summary>
    private void SetTrack(TrackDto track) => Track = track;

    private void ToggleBandInfo() => ShowBandInfo = !ShowBandInfo;

    /// <summary>A lookup settled in the panel: reload the track (status, cover) and keep polling if needed.</summary>
    private async Task OnBandInfoChangedAsync(BandInfoDto info)
    {
        await RefreshAsync(_stop.Token);
        StartPolling();
    }
}
