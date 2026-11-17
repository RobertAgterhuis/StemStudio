using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Repositories;

/// <summary>
/// The values the library revision is computed from (feature 022, data-model 4 <c>LibraryRevision</c>), read in one
/// query: item count, newest item change, newest filing, track count (a deleted track changes art and links), newest
/// track change, joined by <c>|</c> (<c>-</c> for none).
/// </summary>
/// <param name="Value">The joined values.</param>
public sealed record LibraryStamp(string Value);

/// <summary>The picture facts of a track used for folder art (research R-05).</summary>
/// <param name="TrackId">Track.</param>
/// <param name="Artwork">Its displayed picture, or null.</param>
/// <param name="UpdatedAt">Last change of the track (album art tie-break).</param>
public sealed record TrackArtRow(Guid TrackId, TrackArtwork? Artwork, DateTime UpdatedAt);

/// <summary>Data access of the library folders (feature 022): filing, backfill and the folder index.</summary>
public interface ILibraryRepository
{
    /// <summary>A track (the tracked instance when this context already holds it).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The track or null.</returns>
    Task<Track?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Loads tracks into this context (tracked), so <see cref="FindTrackAsync"/> answers them without a query.</summary>
    /// <param name="trackIds">Track ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The ids that exist.</returns>
    Task<IReadOnlyList<Guid>> LoadTracksAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>An item (the tracked instance when this context already holds it).</summary>
    /// <param name="assetId">Item id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The item or null.</returns>
    Task<Asset?> FindAssetAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>The items of a track, tracked.</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<Asset>> ItemsOfTrackAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>Number of items not filed with the current rules version.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The count.</returns>
    Task<int> CountPendingAsync(CancellationToken cancellationToken);

    /// <summary>The oldest items not filed with the current rules version, tracked.</summary>
    /// <param name="limit">Batch size.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<Asset>> PendingAsync(int limit, CancellationToken cancellationToken);

    /// <summary>The revision inputs (one query per table).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stamp.</returns>
    Task<LibraryStamp> StampAsync(CancellationToken cancellationToken);

    /// <summary>Every item, not tracked (folder index).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The items.</returns>
    Task<IReadOnlyList<Asset>> AllItemsAsync(CancellationToken cancellationToken);

    /// <summary>The picture facts of every track, not tracked (folder art).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The rows.</returns>
    Task<IReadOnlyList<TrackArtRow>> TrackArtAsync(CancellationToken cancellationToken);

    /// <summary>Saves the tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ILibraryRepository"/>.</summary>
/// <param name="context">The database context.</param>
public sealed class LibraryRepository(StemStudioDbContext context) : ILibraryRepository
{
    // Constant SQL without any input (one round trip per folder view or search instead of five).
    private static readonly FormattableString StampSql = $"""
        SELECT (SELECT COUNT(*) FROM "Assets") || '|' || IFNULL((SELECT MAX("UpdatedAt") FROM "Assets"), '-') || '|'
            || IFNULL((SELECT MAX("FiledAt") FROM "Assets"), '-') || '|' || (SELECT COUNT(*) FROM "Tracks") || '|'
            || IFNULL((SELECT MAX("UpdatedAt") FROM "Tracks"), '-') AS "Value"
        """;

    /// <inheritdoc />
    public async Task<Track?> FindTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.Tracks.FindAsync([trackId], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Guid>> LoadTracksAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        (await context.Tracks.Where(track => trackIds.Contains(track.Id)).ToListAsync(cancellationToken)).Select(track => track.Id).ToList();

    /// <inheritdoc />
    public async Task<Asset?> FindAssetAsync(Guid assetId, CancellationToken cancellationToken) =>
        await context.Assets.FindAsync([assetId], cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Asset>> ItemsOfTrackAsync(Guid trackId, CancellationToken cancellationToken) =>
        await context.Assets.Where(asset => asset.TrackId == trackId).OrderBy(asset => asset.CreatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<int> CountPendingAsync(CancellationToken cancellationToken) =>
        context.Assets.CountAsync(asset => asset.FiledVersion != LibraryFilingRules.Version, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Asset>> PendingAsync(int limit, CancellationToken cancellationToken) =>
        await context.Assets.Where(asset => asset.FiledVersion != LibraryFilingRules.Version)
            .OrderBy(asset => asset.CreatedAt).ThenBy(asset => asset.Id).Take(limit).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<LibraryStamp> StampAsync(CancellationToken cancellationToken) =>
        new(await context.Database.SqlQuery<string>(StampSql).SingleAsync(cancellationToken));

    /// <inheritdoc />
    public async Task<IReadOnlyList<Asset>> AllItemsAsync(CancellationToken cancellationToken) =>
        await context.Assets.AsNoTracking().ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackArtRow>> TrackArtAsync(CancellationToken cancellationToken) =>
        await context.Tracks.AsNoTracking().Select(track => new TrackArtRow(track.Id, track.Artwork, track.UpdatedAt)).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
