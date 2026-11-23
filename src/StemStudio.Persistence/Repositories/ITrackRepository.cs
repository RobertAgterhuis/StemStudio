using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Repositories;

/// <summary>Storage of <see cref="Track"/> aggregates (one unit of work per scope).</summary>
public interface ITrackRepository
{
    /// <summary>Finds a track by ID, with its lyrics row (feature 006).</summary>
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

    /// <summary>Completed tracks whose quality report is queued or running, for the quality synchroniser (oldest first, feature 020).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListQualityActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// True when the stored detail phase of <paramref name="track"/> still equals the tracked one: no other unit of work
    /// changed it since this one loaded the track (a cancel during a synchroniser pass).
    /// </summary>
    /// <param name="track">A tracked track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>False when the stored phase differs or the track is gone.</returns>
    Task<bool> IsDetailPhaseCurrentAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Tracks whose online lookup is pending or searching, for the metadata synchroniser (oldest first, feature 005).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListLookupActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Tracks whose lyrics are searching, aligning, or waiting for the automatic alignment rule, with their lyrics row, for
    /// the lyrics synchroniser (oldest first, feature 006).
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListLyricsActiveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Queued tracks that never got a worker job and were last changed before <paramref name="updatedBeforeUtc"/>: the
    /// import was interrupted between the insert and the worker call.
    /// </summary>
    /// <param name="updatedBeforeUtc">Cut-off time (UTC, exclusive).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked tracks.</returns>
    Task<IReadOnlyList<Track>> ListOrphanedAsync(DateTime updatedBeforeUtc, CancellationToken cancellationToken);

    /// <summary>Which of the given track IDs still exist (feature 007: <c>Provenance.trackAvailable</c>).</summary>
    /// <param name="ids">Track IDs.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The existing IDs.</returns>
    Task<IReadOnlySet<Guid>> ExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

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

    /// <summary>
    /// Removes a track together with all of its mixes in one unit of work (the mix foreign key is restrict); its artist is
    /// removed in the same save when no other track references it (feature 005, FR-016).
    /// </summary>
    /// <param name="track">Tracked track.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveWithMixesAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Saves changes made to tracked tracks.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}
