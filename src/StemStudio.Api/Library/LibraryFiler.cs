using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>Files library items in the artist &gt; album &gt; song folders (feature 022, research R-04).</summary>
public interface ILibraryFiler
{
    /// <summary>Files one item (call before saving it, in the same save); items that are never filed are left alone.</summary>
    /// <param name="asset">The item (new or tracked).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when the item's song folder changed.</returns>
    Task<bool> FileAsync(Asset asset, CancellationToken cancellationToken);

    /// <summary>Re-files every item of a track after its facts or library info changed (the caller saves).</summary>
    /// <param name="track">The track with its current facts.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of items whose song folder changed.</returns>
    Task<int> RefileTrackAsync(Track track, CancellationToken cancellationToken);

    /// <summary>Files many items (the backfill): their tracks are loaded with one query first.</summary>
    /// <param name="assets">Tracked items.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of items filed (items that are never filed are skipped).</returns>
    Task<int> FileAllAsync(IReadOnlyList<Asset> assets, CancellationToken cancellationToken);

    /// <summary>The resolver input of an item (its track, its source item's placement, its previous placement).</summary>
    /// <param name="asset">The item.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The input.</returns>
    Task<PlacementInput> InputAsync(Asset asset, CancellationToken cancellationToken);
}

/// <summary>Default <see cref="ILibraryFiler"/>: loads the facts, calls <see cref="PlacementResolver"/> and <see cref="Asset.File"/>.</summary>
/// <param name="library">Data access.</param>
/// <param name="clock">Clock.</param>
public sealed class LibraryFiler(ILibraryRepository library, TimeProvider clock) : ILibraryFiler
{
    private readonly HashSet<Guid> _missing = [];

    /// <inheritdoc />
    public async Task<bool> FileAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!LibraryFilingRules.IsFileable(asset.Provenance))
        {
            return false;
        }

        return File(asset, PlacementResolver.Resolve(await InputAsync(asset, cancellationToken)));
    }

    /// <inheritdoc />
    public async Task<int> RefileTrackAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        var moved = 0;
        foreach (var asset in (await library.ItemsOfTrackAsync(track.Id, cancellationToken)).Where(item => LibraryFilingRules.IsFileable(item.Provenance)))
        {
            moved += File(asset, PlacementResolver.Resolve(InputFor(asset, track, null))) ? 1 : 0;
        }

        return moved;
    }

    /// <inheritdoc />
    public async Task<int> FileAllAsync(IReadOnlyList<Asset> assets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(assets);
        var ids = assets.Select(asset => asset.TrackId).OfType<Guid>().Distinct().ToList();
        var found = await library.LoadTracksAsync(ids, cancellationToken);
        _missing.UnionWith(ids.Except(found));
        var fileable = assets.Where(asset => LibraryFilingRules.IsFileable(asset.Provenance)).ToList();
        foreach (var asset in fileable)
        {
            await FileAsync(asset, cancellationToken);
        }

        return fileable.Count;
    }

    /// <inheritdoc />
    public async Task<PlacementInput> InputAsync(Asset asset, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(asset);
        var track = await TrackAsync(asset.TrackId, cancellationToken);
        var source = track is null ? await SourcePlacementAsync(asset, cancellationToken) : null;
        return InputFor(asset, track, source);
    }

    private async Task<Track?> TrackAsync(Guid? trackId, CancellationToken cancellationToken) =>
        trackId is { } id && !_missing.Contains(id) ? await library.FindTrackAsync(id, cancellationToken) : null;

    private static PlacementInput InputFor(Asset asset, Track? track, Placement? source) => new()
    {
        Provenance = asset.Provenance,
        ItemInfo = asset.LibraryInfo,
        TrackInfo = track?.LibraryInfo,
        Track = track is null ? null : TrackFacts.From(track),
        SourcePlacement = source,
        Previous = asset.Placement,
    };

    private async Task<Placement?> SourcePlacementAsync(Asset asset, CancellationToken cancellationToken)
    {
        if (asset.Provenance.SourceAssetIds is not [var sourceId, ..] || await library.FindAssetAsync(sourceId, cancellationToken) is not { } source)
        {
            return null;
        }

        if (source.Placement is { } placement)
        {
            return placement;
        }

        var sourceTrack = await TrackAsync(source.TrackId, cancellationToken);
        return PlacementResolver.Resolve(InputFor(source, sourceTrack, null));
    }

    private bool File(Asset asset, Placement placement)
    {
        var before = asset.Placement?.SongFolderId;
        asset.File(placement, clock.GetUtcNow().UtcDateTime);
        return before != placement.SongFolderId;
    }
}
