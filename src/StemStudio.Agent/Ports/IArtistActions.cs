using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Agent.Ports;

/// <summary>Artwork and band-info endpoints of the public API (feature 005) over loopback.</summary>
public interface IArtistActions
{
    /// <summary>GET /api/tracks/{trackId}/band-info (external text: the result taints the run).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Recording, album and artist facts with sources.</returns>
    Task<BandInfoDto> GetBandInfoAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/metadata/lookup: refreshes artwork and band info (refresh_artwork).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The track with metadata status pending.</returns>
    Task<TrackDto> LookupMetadataAsync(Guid trackId, CancellationToken cancellationToken);
}
