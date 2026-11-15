using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Repositories;

/// <summary>Storage of <see cref="Track"/> aggregates (one unit of work per scope).</summary>
public interface ITrackRepository
{
    /// <summary>Finds a track by ID.</summary>
    /// <param name="id">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked track, or null.</returns>
    Task<Track?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Finds a track by source key.</summary>
    /// <param name="key">Source key.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked track, or null.</returns>
    Task<Track?> FindByKeyAsync(SourceKey key, CancellationToken cancellationToken);

    /// <summary>All tracks, newest <see cref="Track.UpdatedAt"/> first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracks (not tracked).</returns>
    Task<IReadOnlyList<Track>> ListAsync(CancellationToken cancellationToken);

    /// <summary>Non-terminal tracks that have a worker job, for the job synchroniser.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>Completed tracks whose detail phase is queued or running, for the detail synchroniser (oldest first).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListDetailActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Queued tracks that never got a worker job and were last changed before <paramref name="updatedBeforeUtc"/>: the
    /// import was interrupted between the insert and the worker call.
    /// </summary>
    /// <param name="updatedBeforeUtc">Cut-off time (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListOrphanedAsync(DateTime updatedBeforeUtc, CancellationToken cancellationToken);

    /// <summary>Adds a track and saves.</summary>
    /// <param name="track">New track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">A track with the same source key exists.</exception>
    Task AddAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Removes a track and saves.</summary>
    /// <param name="track">Tracked track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Removes a track together with all of its mixes in one unit of work (the mix foreign key is restrict).</summary>
    /// <param name="track">Tracked track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveWithMixesAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Saves changes made to tracked tracks.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}
