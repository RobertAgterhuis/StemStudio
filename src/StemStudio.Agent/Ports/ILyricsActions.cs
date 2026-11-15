namespace StemStudio.Agent.Ports;

/// <summary>
/// Lyrics endpoints of the public API (feature 006) over loopback. Every member returns a <see cref="LyricsSummary"/>:
/// the implementation drops the lyrics lines, so lyrics text never reaches the agent library (FR-026).
/// </summary>
public interface ILyricsActions
{
    /// <summary>GET /api/tracks/{trackId}/lyrics, reduced to its state.</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics state.</returns>
    Task<LyricsSummary> GetLyricsSummaryAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/lyrics/search (fetch_lyrics, job).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics state (searching).</returns>
    Task<LyricsSummary> SearchLyricsAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>POST /api/tracks/{trackId}/lyrics/align (align_lyrics, heavy GPU job).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics state (aligning).</returns>
    Task<LyricsSummary> AlignLyricsAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>DELETE /api/tracks/{trackId}/lyrics (compensation of fetch_lyrics).</summary>
    /// <param name="trackId">Track identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The lyrics state (removed).</returns>
    Task<LyricsSummary> RemoveLyricsAsync(Guid trackId, CancellationToken cancellationToken);
}
