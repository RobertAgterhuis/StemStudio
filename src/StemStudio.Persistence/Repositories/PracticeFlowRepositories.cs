using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Practice;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Setlists;

namespace StemStudio.Persistence.Repositories;

/// <summary>Persistence of section sets (feature 014).</summary>
public interface ISectionRepository
{
    /// <summary>The section set of a track with its sections (tracked).</summary>
    /// <param name="trackId">Track id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The set, or null when never created.</returns>
    Task<TrackSections?> FindAsync(Guid trackId, CancellationToken cancellationToken);

    /// <summary>The sets with a queued or running detection (tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The sets.</returns>
    Task<IReadOnlyList<TrackSections>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>Section counts per track.</summary>
    /// <param name="trackIds">Tracks.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Count per track (tracks without sections are missing).</returns>
    Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken);

    /// <summary>Inserts a set and saves.</summary>
    /// <param name="sections">The set.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(TrackSections sections, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="ISectionRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class SectionRepository(StemStudioDbContext context) : ISectionRepository
{
    private static readonly SectionSetStatus[] Active = [SectionSetStatus.Queued, SectionSetStatus.Detecting];

    /// <inheritdoc />
    public Task<TrackSections?> FindAsync(Guid trackId, CancellationToken cancellationToken) =>
        context.TrackSections.Include(set => set.Sections).AsSplitQuery().FirstOrDefaultAsync(set => set.TrackId == trackId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<TrackSections>> ActiveAsync(CancellationToken cancellationToken) =>
        await context.TrackSections.Where(set => Active.Contains(set.Status)).OrderBy(set => set.UpdatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> CountsAsync(IReadOnlyCollection<Guid> trackIds, CancellationToken cancellationToken) =>
        await context.Sections.Where(section => trackIds.Contains(section.TrackId)).GroupBy(section => section.TrackId)
            .ToDictionaryAsync(group => group.Key, group => group.Count(), cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(TrackSections sections, CancellationToken cancellationToken)
    {
        context.TrackSections.Add(sections);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

/// <summary>Persistence of score views (feature 014).</summary>
public interface IScoreViewRepository
{
    /// <summary>The view of a score asset (tracked).</summary>
    /// <param name="assetId">Asset id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The view, or null.</returns>
    Task<ScoreView?> FindAsync(Guid assetId, CancellationToken cancellationToken);

    /// <summary>The views of several assets (not tracked).</summary>
    /// <param name="assetIds">Asset ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>View per asset id.</returns>
    Task<IReadOnlyDictionary<Guid, ScoreView>> FindManyAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken);

    /// <summary>The queued or rendering views (tracked).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The views.</returns>
    Task<IReadOnlyList<ScoreView>> ActiveAsync(CancellationToken cancellationToken);

    /// <summary>Inserts a view and saves.</summary>
    /// <param name="view">The view.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(ScoreView view, CancellationToken cancellationToken);

    /// <summary>Saves tracked changes.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IScoreViewRepository"/>.</summary>
/// <param name="context">The context.</param>
public sealed class ScoreViewRepository(StemStudioDbContext context) : IScoreViewRepository
{
    private static readonly ScoreViewStatus[] Active = [ScoreViewStatus.Queued, ScoreViewStatus.Rendering];

    /// <inheritdoc />
    public Task<ScoreView?> FindAsync(Guid assetId, CancellationToken cancellationToken) =>
        context.ScoreViews.FirstOrDefaultAsync(view => view.AssetId == assetId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, ScoreView>> FindManyAsync(IReadOnlyCollection<Guid> assetIds, CancellationToken cancellationToken) =>
        await context.ScoreViews.AsNoTracking().Where(view => assetIds.Contains(view.AssetId)).ToDictionaryAsync(view => view.AssetId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<ScoreView>> ActiveAsync(CancellationToken cancellationToken) =>
        await context.ScoreViews.Where(view => Active.Contains(view.Status)).OrderBy(view => view.UpdatedAt).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(ScoreView view, CancellationToken cancellationToken)
    {
        context.ScoreViews.Add(view);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}

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
