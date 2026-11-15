using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Agent.Ports;

/// <summary>
/// Track, import, detail and device endpoints of the public API (features 001-003), called over loopback by the backend's
/// implementation so every API validator applies (research R-01). Failures are <see cref="AgentActionException"/>.
/// </summary>
public interface ITrackActions
{
    /// <summary>GET /api/tracks.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>All tracks, newest first.</returns>
    Task<IReadOnlyList<TrackDto>> ListTracksAsync(CancellationToken cancellationToken);

    /// <summary>GET /api/tracks/{trackId}.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The track.</returns>
    Task<TrackDto> GetTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/imports (job; the URL must come from a user message, FR-014).</summary>
    /// <param name="url">YouTube or Spotify track URL.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The new (or cached) track.</returns>
    Task<TrackDto> ImportFromUrlAsync(string url, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/retry.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The queued track.</returns>
    Task<TrackDto> RetryTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>DELETE /api/tracks/{trackId} (destructive).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/detail (heavy).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The track with the queued detail phase.</returns>
    Task<TrackDto> StartDetailAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/detail/cancel.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The track.</returns>
    Task<TrackDto> CancelDetailAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>GET /api/device.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The worker's processing device.</returns>
    Task<ProcessingDeviceDto> GetDeviceAsync(CancellationToken cancellationToken);
}
