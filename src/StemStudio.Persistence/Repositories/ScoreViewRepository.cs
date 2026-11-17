using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Practice;

namespace StemStudio.Persistence.Repositories;

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

    /// <summary>Adds a view to the scope; saved with the next save (used inside the asset sync).</summary>
    /// <param name="view">The view.</param>
    void Add(ScoreView view);

    /// <summary>Every ready score item (not tracked), newest first.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The score items.</returns>
    Task<IReadOnlyList<Asset>> ReadyScoresAsync(CancellationToken cancellationToken);

    /// <summary>Items by id (not tracked).</summary>
    /// <param name="ids">Item ids.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>Item per id.</returns>
    Task<IReadOnlyDictionary<Guid, Asset>> AssetsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

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
    public void Add(ScoreView view) => context.ScoreViews.Add(view);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Asset>> ReadyScoresAsync(CancellationToken cancellationToken) =>
        (await context.Assets.AsNoTracking().Where(asset => asset.Kind == AssetKind.Score && asset.Status == AssetStatus.Ready).ToListAsync(cancellationToken))
            .OrderByDescending(asset => asset.CreatedAt).ThenBy(asset => asset.Id).ToList();

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, Asset>> AssetsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await context.Assets.AsNoTracking().Where(asset => ids.Contains(asset.Id)).ToDictionaryAsync(asset => asset.Id, cancellationToken);

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);
}
