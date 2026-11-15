using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Artists;

namespace StemStudio.Persistence.Repositories;

/// <summary>Storage of <see cref="Artist"/> rows (feature 005; shares the unit of work of the scope).</summary>
public interface IArtistRepository
{
    /// <summary>Finds an artist by MusicBrainz id.</summary>
    /// <param name="mbid">MusicBrainz artist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked artist, or null.</returns>
    Task<Artist?> FindAsync(Guid mbid, CancellationToken cancellationToken);

    /// <summary>Adds an artist; it is saved with the next save of the scope (track or artist repository).</summary>
    /// <param name="artist">New artist.</param>
    void Add(Artist artist);

    /// <summary>Deletes the artist and saves when no track references it any more (FR-016).</summary>
    /// <param name="mbid">MusicBrainz artist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when a row was deleted.</returns>
    Task<bool> RemoveIfUnusedAsync(Guid mbid, CancellationToken cancellationToken);

    /// <summary>Saves the changes of the scope.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IArtistRepository"/>.</summary>
/// <param name="context">The scoped context.</param>
public sealed class ArtistRepository(StemStudioDbContext context) : IArtistRepository
{
    /// <inheritdoc />
    public async Task<Artist?> FindAsync(Guid mbid, CancellationToken cancellationToken) =>
        await context.Artists.FirstOrDefaultAsync(artist => artist.Mbid == mbid, cancellationToken);

    /// <inheritdoc />
    public void Add(Artist artist) => context.Artists.Add(artist);

    /// <inheritdoc />
    public async Task<bool> RemoveIfUnusedAsync(Guid mbid, CancellationToken cancellationToken)
    {
        var artist = await FindAsync(mbid, cancellationToken);
        if (artist is null || await context.Tracks.AnyAsync(track => track.ArtistId == mbid, cancellationToken))
        {
            return false;
        }

        context.Artists.Remove(artist);
        await UnitOfWork.SaveAsync(context, cancellationToken);
        return true;
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
