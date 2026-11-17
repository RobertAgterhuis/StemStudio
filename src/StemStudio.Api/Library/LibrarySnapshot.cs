using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>One filed (or preview-filed) item of a snapshot with its folder ids and search terms.</summary>
/// <param name="Asset">The item (not tracked).</param>
/// <param name="Placement">Its placement (the automatic preview while not filed).</param>
/// <param name="Terms">Its instrument terms (research R-08).</param>
public sealed record LibraryEntry(Asset Asset, Placement Placement, InstrumentTerms Terms)
{
    /// <summary>Artist folder id.</summary>
    public string ArtistId { get; } = Placement.ArtistFolderId;

    /// <summary>Album folder id.</summary>
    public string AlbumId { get; } = Placement.AlbumFolderId;

    /// <summary>Song folder id.</summary>
    public string SongId { get; } = Placement.SongFolderId;

    /// <summary>The folder id of a level.</summary>
    /// <param name="level">Folder level.</param>
    /// <returns>The id.</returns>
    public string IdOf(FolderLevel level) => level switch
    {
        FolderLevel.Artist => ArtistId,
        FolderLevel.Album => AlbumId,
        _ => SongId,
    };

    /// <summary>When the item was filed (its creation time while not filed); ties of the display name rule.</summary>
    public DateTime FiledOrder => Asset.FiledAt ?? Asset.CreatedAt;
}

/// <summary>
/// The library at one revision (feature 022, data-model 4 <c>LibraryIndex</c>): every filed item with its placement, the
/// track pictures and the folder membership per level. Immutable; rebuilt when the revision changes.
/// </summary>
public sealed class LibrarySnapshot
{
    private readonly Dictionary<string, List<LibraryEntry>> _folders;

    private LibrarySnapshot(string revision, IReadOnlyList<LibraryEntry> entries, IReadOnlyDictionary<Guid, TrackArtRow> tracks, int pending)
    {
        (Revision, Entries, Tracks, PendingFiling) = (revision, entries, tracks, pending);
        _folders = entries.SelectMany(entry => new[] { (entry.ArtistId, entry), (entry.AlbumId, entry), (entry.SongId, entry) })
            .GroupBy(pair => pair.Item1, pair => pair.entry, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
    }

    /// <summary>Library revision (16 hex).</summary>
    public string Revision { get; }

    /// <summary>Every fileable item.</summary>
    public IReadOnlyList<LibraryEntry> Entries { get; }

    /// <summary>Existing tracks with their pictures.</summary>
    public IReadOnlyDictionary<Guid, TrackArtRow> Tracks { get; }

    /// <summary>Items not filed with the current rules yet.</summary>
    public int PendingFiling { get; }

    /// <summary>Builds a snapshot.</summary>
    /// <param name="revision">Library revision.</param>
    /// <param name="items">Every item.</param>
    /// <param name="tracks">Every track's picture facts.</param>
    /// <returns>The snapshot.</returns>
    public static LibrarySnapshot Build(string revision, IReadOnlyList<Asset> items, IReadOnlyList<TrackArtRow> tracks)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(tracks);
        var fileable = items.Where(asset => LibraryFilingRules.IsFileable(asset.Provenance)).ToList();
        var entries = fileable.Select(asset => new LibraryEntry(asset, PlacementOf(asset), InstrumentTerms.Of(asset))).ToList();
        return new LibrarySnapshot(revision, entries, tracks.ToDictionary(track => track.TrackId), fileable.Count(asset => !asset.IsFiled));
    }

    /// <summary>The items of a folder (any level).</summary>
    /// <param name="folderId">Folder id.</param>
    /// <returns>The items; empty for an unknown folder.</returns>
    public IReadOnlyList<LibraryEntry> In(string folderId) => _folders.TryGetValue(folderId, out var entries) ? entries : [];

    /// <summary>True when the item's track still exists.</summary>
    /// <param name="asset">The item.</param>
    /// <returns>True when its track exists.</returns>
    public bool TrackExists(Asset asset) => asset?.TrackId is { } trackId && Tracks.ContainsKey(trackId);

    private static Placement PlacementOf(Asset asset) => asset.IsFiled
        ? asset.Placement!
        : PlacementResolver.Resolve(new PlacementInput { Provenance = asset.Provenance, ItemInfo = asset.LibraryInfo, Previous = asset.Placement });
}
