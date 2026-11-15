using Microsoft.EntityFrameworkCore;
using StemStudio.Domain.Assets;

namespace StemStudio.Persistence.Repositories;

/// <summary>Filters and paging of the library (backend <c>listAssets</c>).</summary>
public sealed record AssetQuery
{
    /// <summary>Only this kind.</summary>
    public AssetKind? Kind { get; init; }

    /// <summary>Only items made from this track.</summary>
    public Guid? TrackId { get; init; }

    /// <summary>Only this status.</summary>
    public AssetStatus? Status { get; init; }

    /// <summary>Case-insensitive "name contains".</summary>
    public string? NameContains { get; init; }

    /// <summary>Page size.</summary>
    public int Limit { get; init; } = 100;

    /// <summary>Items to skip.</summary>
    public int Offset { get; init; }
}

/// <summary>One page of the library.</summary>
/// <param name="Items">The items, newest first.</param>
/// <param name="Total">Items matching the filters.</param>
/// <param name="TotalSizeBytes">Size of the items matching the filters.</param>
public sealed record AssetPage(IReadOnlyList<Asset> Items, int Total, long TotalSizeBytes);

/// <summary>Storage of <see cref="Asset"/> items (feature 007; one unit of work per scope).</summary>
public interface IAssetRepository
{
    /// <summary>Finds an item.</summary>
    /// <param name="id">Item ID.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked item, or null.</returns>
    Task<Asset?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>One page of items matching the filters, newest first.</summary>
    /// <param name="query">Filters and paging.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The page (not tracked).</returns>
    Task<AssetPage> ListAsync(AssetQuery query, CancellationToken cancellationToken);

    /// <summary>Queued and rendering items, for the asset synchroniser.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The tracked items.</returns>
    Task<IReadOnlyList<Asset>> ListActiveAsync(CancellationToken cancellationToken);

    /// <summary>Every item name (for unique default names).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The names.</returns>
    Task<IReadOnlyList<string>> NamesAsync(CancellationToken cancellationToken);

    /// <summary>Adds an item; saved with the next save of the scope.</summary>
    /// <param name="asset">New item.</param>
    void Add(Asset asset);

    /// <summary>Removes an item and saves.</summary>
    /// <param name="asset">Tracked item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task RemoveAsync(Asset asset, CancellationToken cancellationToken);

    /// <summary>Saves the changes of the scope.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>EF Core implementation of <see cref="IAssetRepository"/>.</summary>
/// <param name="context">The scoped context.</param>
public sealed class AssetRepository(StemStudioDbContext context) : IAssetRepository
{
    /// <inheritdoc />
    public async Task<Asset?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        await context.Assets.FirstOrDefaultAsync(asset => asset.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<AssetPage> ListAsync(AssetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var matching = Filter(context.Assets.AsNoTracking(), query);
        var items = await matching.OrderByDescending(asset => asset.CreatedAt).Skip(query.Offset).Take(query.Limit).ToListAsync(cancellationToken);
        var total = await matching.CountAsync(cancellationToken);
        var size = await matching.SumAsync(asset => asset.SizeBytes, cancellationToken);
        return new AssetPage(items, total, size);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Asset>> ListActiveAsync(CancellationToken cancellationToken) =>
        await context.Assets
            .Where(asset => asset.Status == AssetStatus.Queued || asset.Status == AssetStatus.Rendering)
            .OrderBy(asset => asset.CreatedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> NamesAsync(CancellationToken cancellationToken) =>
        await context.Assets.Select(asset => asset.Name).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Add(Asset asset) => context.Assets.Add(asset);

    /// <inheritdoc />
    public async Task RemoveAsync(Asset asset, CancellationToken cancellationToken)
    {
        context.Assets.Remove(asset);
        await UnitOfWork.SaveAsync(context, cancellationToken);
    }

    /// <inheritdoc />
    public Task SaveAsync(CancellationToken cancellationToken) => UnitOfWork.SaveAsync(context, cancellationToken);

    private static IQueryable<Asset> Filter(IQueryable<Asset> assets, AssetQuery query)
    {
        var pattern = query.NameContains is { } text ? $"%{Escape(text)}%" : null;
        return FilterValues(assets, query).Where(asset => pattern == null || EF.Functions.Like(asset.Name, pattern, "\\"));
    }

    private static IQueryable<Asset> FilterValues(IQueryable<Asset> assets, AssetQuery query) => assets
        .Where(asset => query.Kind == null || asset.Kind == query.Kind)
        .Where(asset => query.TrackId == null || asset.TrackId == query.TrackId)
        .Where(asset => query.Status == null || asset.Status == query.Status);

    /// <summary>Escapes the LIKE wildcards; SQLite LIKE ignores ASCII case.</summary>
    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);
}
