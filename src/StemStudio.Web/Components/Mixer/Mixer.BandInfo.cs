using StemStudio.Contracts.Artists;
using StemStudio.Web.Api;

namespace StemStudio.Web.Components.Mixer;

/// <summary>Band info panel and album line of the mixer header (feature 005, US2).</summary>
public sealed partial class Mixer
{
    /// <summary>True while the band info panel is shown.</summary>
    private bool ShowBandInfo { get; set; }

    private void ToggleBandInfo() => ShowBandInfo = !ShowBandInfo;

    /// <summary>
    /// A lookup settled in the panel: take the new picture and metadata of the track. The loaded stems, beat grid and mix
    /// state stay untouched; a failed refresh keeps the current header.
    /// </summary>
    private async Task RefreshTrackAsync(BandInfoDto info)
    {
        try
        {
            var fresh = await Api.GetTrackAsync(Id);
            _track = _track! with { Artwork = fresh.Artwork, Metadata = fresh.Metadata };
        }
        catch (ApiException)
        {
            // keep the current header; the next open of the mixer shows the new state
        }
    }
}
