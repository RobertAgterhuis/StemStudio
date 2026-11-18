using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Extension point of the detail sync: runs after a detail phase completed and the track was saved (feature 016 drum hits
/// after the drums split). A failing hook is logged and never undoes the completion.
/// </summary>
public interface IDetailCompletionHook
{
    /// <summary>Called once per completed detail phase.</summary>
    /// <param name="track">The track with its completed, saved detail phase.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task OnDetailCompletedAsync(Track track, CancellationToken cancellationToken);
}
