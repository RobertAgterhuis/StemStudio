using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Extension point of the job sync: runs after a separation completed and the track was saved (feature 014 section
/// detection). A failing hook is logged and never undoes the completion.
/// </summary>
public interface ITrackCompletionHook
{
    /// <summary>Called once per completed separation.</summary>
    /// <param name="track">The completed, saved track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task OnCompletedAsync(Track track, CancellationToken cancellationToken);
}
