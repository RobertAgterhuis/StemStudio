namespace StemStudio.Api.Tracks;

/// <summary>Deletes the takes of a track inside the track delete (FR-018): staged in the same unit of work, files afterwards.</summary>
public interface ITrackTakesCleanup
{
    /// <summary>Removes the takes of a track and their items from the scope (not saved) and cancels running renders.</summary>
    /// <param name="trackId">The track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The asset ids whose folders and hashes go after the save.</returns>
    Task<IReadOnlyList<Guid>> StageAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Deletes the folders and audio hashes of removed take items (best effort for the files).</summary>
    /// <param name="assetIds">From <see cref="StageAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task DeleteFilesAsync(IReadOnlyList<Guid> assetIds, CancellationToken cancellationToken);
}
