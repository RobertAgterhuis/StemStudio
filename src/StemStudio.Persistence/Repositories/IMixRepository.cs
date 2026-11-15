using StemStudio.Domain.Mixes;

namespace StemStudio.Persistence.Repositories;

/// <summary>A mix-library entry with the title of its track.</summary>
/// <param name="Id">Mix ID.</param>
/// <param name="TrackId">Track ID.</param>
/// <param name="TrackTitle">Track title.</param>
/// <param name="Name">Mix name.</param>
/// <param name="UpdatedAt">Last modification (UTC).</param>
public sealed record MixSummary(Guid Id, Guid TrackId, string TrackTitle, string Name, DateTime UpdatedAt);

/// <summary>Storage of <see cref="Mix"/> aggregates (one unit of work per scope).</summary>
public interface IMixRepository
{
    /// <summary>Finds a mix by ID.</summary>
    /// <param name="id">Mix ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked mix, or null.</returns>
    Task<Mix?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Mix-library entries, newest first.</summary>
    /// <param name="trackId">Only mixes of this track, or null for all.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<MixSummary>> ListAsync(Guid? trackId, CancellationToken cancellationToken);

    /// <summary>Names of all mixes of a track.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The names.</returns>
    Task<IReadOnlyList<string>> NamesForTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>True when another mix of the track has this normalised name.</summary>
    /// <param name="trackId">Track ID.</param>
    /// <param name="normalizedName">Normalised name (see <see cref="MixNaming.Normalize"/>).</param>
    /// <param name="excludeMixId">A mix to ignore (the one being renamed), or null.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when taken.</returns>
    Task<bool> IsNameTakenAsync(Guid trackId, string normalizedName, Guid? excludeMixId, CancellationToken cancellationToken);

    /// <summary>Adds a mix and saves.</summary>
    /// <param name="mix">New mix.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">The name is taken for the track.</exception>
    Task AddAsync(Mix mix, CancellationToken cancellationToken);

    /// <summary>Removes a mix and saves.</summary>
    /// <param name="mix">Tracked mix.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Mix mix, CancellationToken cancellationToken);

    /// <summary>Saves changes made to tracked mixes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    /// <exception cref="DuplicateKeyException">A rename clashes with another mix of the track.</exception>
    Task SaveAsync(CancellationToken cancellationToken);
}
