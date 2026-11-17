using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Mixes;
using StemStudio.Domain.Setlists;
using StemStudio.Domain.Tracks;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of setlists (feature 014).</summary>
public interface ISetlistRepository
{
    /// <summary>All setlists with their entries, most recently updated first (not tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The setlists.</returns>
    Task<IReadOnlyList<Setlist>> ListAsync(CancellationToken cancellationToken);

    /// <summary>One setlist with its entries (tracked).</summary>
    /// <param name="id">Setlist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The setlist, or null.</returns>
    Task<Setlist?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Inserts a setlist and saves.</summary>
    /// <param name="setlist">The setlist.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(Setlist setlist, CancellationToken cancellationToken);

    /// <summary>Tracks by id with their lyrics (not tracked).</summary>
    /// <param name="ids">Track ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Track per id (missing ids are left out).</returns>
    Task<IReadOnlyDictionary<Guid, Track>> TracksAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Mixes by id (not tracked).</summary>
    /// <param name="ids">Mix ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Mix per id (missing ids are left out).</returns>
    Task<IReadOnlyDictionary<Guid, Mix>> MixesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Deletes a setlist (entries cascade) and saves.</summary>
    /// <param name="setlist">The setlist.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Setlist setlist, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ISetlistRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class SetlistRepository(StemStudioDbContext context) : ISetlistRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<Setlist>> ListAsync(CancellationToken cancellationToken) =>
        (await context.Setlists.AsNoTracking().Include(setlist => setlist.Entries).AsSplitQuery().ToListAsync(cancellationToken))
            .OrderByDescending(setlist => setlist.UpdatedAt).ThenBy(setlist => setlist.Id).ToList();

    /// <inheritdoc />
    public Task<Setlist?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        context.Setlists.Include(setlist => setlist.Entries).AsSplitQuery().FirstOrDefaultAsync(setlist => setlist.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(Setlist setlist, CancellationToken cancellationToken)
    {
        context.Setlists.Add(setlist);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Track>> TracksAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.Tracks.AsNoTracking().Include(track => track.Lyrics).Where(track => ids.Contains(track.Id)).ToDictionaryAsync(track => track.Id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Mix>> MixesAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.Mixes.AsNoTracking().Where(mix => ids.Contains(mix.Id)).ToDictionaryAsync(mix => mix.Id, cancellationToken);

    /// <inheritdoc />
    public async Task RemoveAsync(Setlist setlist, CancellationToken cancellationToken)
    {
        context.Setlists.Remove(setlist);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

/// <summary>Counts the setlist entries a track deletion removes (data-model 3.6 <c>RemovedEntries</c>).</summary>
internal static class SetlistRemovals
{
    /// <summary>Adds the track's entry count to every affected setlist (tracked; saved with the deletion).</summary>
    /// <param name="context">The context.</param>
    /// <param name="trackId">The track being deleted.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public static async Task CountAsync(StemStudioDbContext context, Guid trackId, CancellationToken cancellationToken)
    {
        var counts = await context.SetlistEntries.Where(entry => entry.TrackId == trackId).GroupBy(entry => entry.SetlistId)
            .Select(group => new { SetlistId = group.Key, Count = group.Count() }).ToListAsync(cancellationToken);
        var ids = counts.Select(count => count.SetlistId).ToList();
        var setlists = await context.Setlists.Where(setlist => ids.Contains(setlist.Id)).ToDictionaryAsync(setlist => setlist.Id, cancellationToken);
        foreach (var count in counts)
        {
            setlists[count.SetlistId].CountRemoved(count.Count);
        }
    }
}
