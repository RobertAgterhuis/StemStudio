using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ITrackRepository"/>.</summary>
/// <param name="context">The scoped context.</param>
public sealed class TrackRepository(StemStudioDbContext context) : ITrackRepository
{
    /// <inheritdoc />
    public async Task<Track?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Tracks.FirstOrDefaultAsync(track => track.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<Track?> FindByKeyAsync(SourceKey key, CancellationToken cancellationToken) =>
        await context.Tracks.FirstOrDefaultAsync(track => track.Key == key, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListAsync(CancellationToken cancellationToken) =>
        await context.Tracks.AsNoTracking().OrderByDescending(track => track.UpdatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.Tracks
            .Where(track => track.WorkerJobId != null && track.Status != TrackStatus.Completed && track.Status != TrackStatus.Failed)
            .OrderBy(track => track.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListDetailActiveAsync(CancellationToken cancellationToken)
    {
        // The phase is one JSON column; its status is filtered in memory (a local library has few tracks).
        var withPhase = await context.Tracks
            .Where(track => track.Status == TrackStatus.Completed && track.DetailPhase != null)
            .OrderBy(track => track.CreatedAt)
            .ToListAsync(cancellationToken);
        return withPhase.Where(track => track.IsDetailActive).ToList();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListLookupActiveAsync(CancellationToken cancellationToken) =>
        await context.Tracks
            .Where(track => track.Metadata.Status == MetadataStatus.Pending || track.Metadata.Status == MetadataStatus.Searching)
            .OrderBy(track => track.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Track>> ListOrphanedAsync(DateTime updatedBeforeUtc, CancellationToken cancellationToken) =>
        await context.Tracks
            .Where(track => track.WorkerJobId == null && track.Status == TrackStatus.Queued && track.UpdatedAt < updatedBeforeUtc)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Track track, CancellationToken cancellationToken)
    {
        context.Tracks.Add(track);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Track track, CancellationToken cancellationToken)
    {
        context.Tracks.Remove(track);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveWithMixesAsync(Track track, CancellationToken cancellationToken)
    {
        context.Mixes.RemoveRange(await context.Mixes.Where(mix => mix.TrackId == track.Id).ToListAsync(cancellationToken));
        await RemoveOrphanedArtistAsync(track, cancellationToken);
        context.Tracks.Remove(track);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    private async Task RemoveOrphanedArtistAsync(Track track, CancellationToken cancellationToken)
    {
        if (track.ArtistId is not { } artistId
            || await context.Tracks.AnyAsync(other => other.ArtistId == artistId && other.Id != track.Id, cancellationToken))
        {
            return;
        }

        context.Artists.RemoveRange(await context.Artists.Where(artist => artist.Mbid == artistId).ToListAsync(cancellationToken));
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
