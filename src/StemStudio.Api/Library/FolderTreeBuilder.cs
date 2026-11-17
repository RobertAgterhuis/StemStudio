using StemStudio.Api.Assets;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>A folder view request (backend-api 1.22.0 <c>getLibraryRoot</c>/<c>getLibraryFolder</c>), already validated.</summary>
public sealed record FolderQuery
{
    /// <summary>The folder, or null for the root.</summary>
    public string? FolderId { get; init; }

    /// <summary>Child order, or null for the level's default.</summary>
    public FolderSortDto? Sort { get; init; }

    /// <summary>Only items of this kind.</summary>
    public AssetKind? Kind { get; init; }

    /// <summary>Only items with this status.</summary>
    public AssetStatus? Status { get; init; }

    /// <summary>Child folders per page.</summary>
    public int Limit { get; init; } = ContractLimits.LibraryFolderPageDefault;

    /// <summary>Child folders to skip.</summary>
    public int Offset { get; init; }
}

/// <summary>
/// Builds folder views from a <see cref="LibrarySnapshot"/> (feature 022, FR-007, FR-011, FR-013..FR-018): display
/// names, counts and sizes per level, art, the default and Advanced sorts, paging, and the items of a song folder in
/// mixer order. Folders are derived: a folder exists while it holds an item. Pure.
/// </summary>
public static class FolderTreeBuilder
{
    /// <summary>The view of the root or of a folder.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="query">The request.</param>
    /// <returns>The view, or null when the folder holds no item (any more).</returns>
    public static LibraryFolderViewDto? View(LibrarySnapshot snapshot, FolderQuery query)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(query);
        if (query.FolderId is not { } id)
        {
            return Root(snapshot, query);
        }

        var all = snapshot.In(id);
        return all.Count > 0 && FolderId.TryParse(id, out var level) ? FolderView(snapshot, query, level, all) : null;
    }

    /// <summary>A folder of a level from all its items (name, art) and the counted items (counts, size).</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="level">Folder level.</param>
    /// <param name="all">Every item of the folder (at least one).</param>
    /// <param name="counted">The items that match the filters.</param>
    /// <returns>The folder.</returns>
    public static LibraryFolderDto Folder(LibrarySnapshot snapshot, FolderLevel level, IReadOnlyList<LibraryEntry> all, IReadOnlyList<LibraryEntry> counted)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(all);
        ArgumentNullException.ThrowIfNull(counted);
        var id = all[0].IdOf(level);
        var (name, source) = DisplayName(all, level);
        return new LibraryFolderDto
        {
            Id = id,
            Level = LibraryMapper.ToDto(level),
            Name = name,
            NameSource = LibraryMapper.ToDto(source),
            Year = level == FolderLevel.Album ? YearOf(all) : null,
            Counts = Counts(level, counted),
            SizeBytes = counted.Sum(entry => entry.Asset.SizeBytes),
            LatestAt = TrackMapper.Utc((counted.Count > 0 ? counted : all).Max(entry => entry.Asset.CreatedAt)),
            Art = FolderArtPicker.Art(Images(snapshot, level, all), id, name, source),
            TrackIds = level == FolderLevel.Song ? TrackIdsOf(snapshot, all) : [],
        };
    }

    /// <summary>The parents of a folder from the artist down (breadcrumb, search paths).</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="entry">An item inside the folder.</param>
    /// <param name="level">The folder's level.</param>
    /// <returns>0..2 references.</returns>
    public static IReadOnlyList<FolderRefDto> ParentsOf(LibrarySnapshot snapshot, LibraryEntry entry, FolderLevel level)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return [.. new[] { FolderLevel.Artist, FolderLevel.Album }.Where(parent => parent < level).Select(parent => RefOf(snapshot, entry, parent))];
    }

    /// <summary>A reference to the folder of an item at a level.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <param name="entry">An item inside the folder.</param>
    /// <param name="level">Folder level.</param>
    /// <returns>The reference with the folder's display name.</returns>
    public static FolderRefDto RefOf(LibrarySnapshot snapshot, LibraryEntry entry, FolderLevel level)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(entry);
        var id = entry.IdOf(level);
        return new FolderRefDto { Id = id, Level = LibraryMapper.ToDto(level), Name = DisplayName(snapshot.In(id), level).Name };
    }

    /// <summary>Items in mixer order (standard stems, detail stems, mixes, sheet music, generated takes), then newest first.</summary>
    /// <param name="entries">The items.</param>
    /// <returns>The ordered items.</returns>
    public static IEnumerable<LibraryEntry> InMixerOrder(IEnumerable<LibraryEntry> entries) =>
        entries.OrderBy(entry => entry.Terms.Group).ThenBy(entry => entry.Terms.Order)
            .ThenByDescending(entry => entry.Asset.CreatedAt).ThenBy(entry => entry.Asset.Id);

    /// <summary>The display name of a folder (FR-011): the match spelling, else the most used, ties the earliest filed.</summary>
    /// <param name="entries">The folder's items (at least one).</param>
    /// <param name="level">Folder level.</param>
    /// <returns>The name and its source.</returns>
    public static (string Name, PlacementSource Source) DisplayName(IReadOnlyList<LibraryEntry> entries, FolderLevel level)
    {
        ArgumentNullException.ThrowIfNull(entries);
        var ordered = entries.OrderBy(entry => entry.FiledOrder).ThenBy(entry => entry.Asset.Id).Select(entry => entry.Placement.Of(level)).ToList();
        var chosen = ordered.FirstOrDefault(placement => placement.Source == PlacementSource.Match)
            ?? ordered.GroupBy(placement => placement.Name, StringComparer.Ordinal).OrderByDescending(group => group.Count()).First().First();
        return (chosen.Name, chosen.Source);
    }

    private static LibraryFolderViewDto Root(LibrarySnapshot snapshot, FolderQuery query)
    {
        var counted = Filter(snapshot.Entries, query);
        return Page(snapshot, query, counted, FolderLevel.Artist) with { Folder = null };
    }

    private static LibraryFolderViewDto FolderView(LibrarySnapshot snapshot, FolderQuery query, FolderLevel level, IReadOnlyList<LibraryEntry> all)
    {
        var counted = Filter(all, query);
        var view = level == FolderLevel.Song ? SongPage(snapshot, counted) : Page(snapshot, query, counted, level + 1);
        return view with { Folder = Folder(snapshot, level, all, counted), Breadcrumb = ParentsOf(snapshot, all[0], level) };
    }

    private static LibraryFolderViewDto Page(LibrarySnapshot snapshot, FolderQuery query, IReadOnlyList<LibraryEntry> counted, FolderLevel childLevel)
    {
        var children = counted.GroupBy(entry => entry.IdOf(childLevel), StringComparer.Ordinal)
            .Select(group => Folder(snapshot, childLevel, snapshot.In(group.Key), [.. group]));
        var sorted = FolderSorting.Sort(children, childLevel, query.Sort ?? FolderSorting.DefaultFor(childLevel)).ToList();
        return Totals(snapshot, counted) with { Children = [.. sorted.Skip(query.Offset).Take(query.Limit)], ChildrenTotal = sorted.Count };
    }

    private static LibraryFolderViewDto SongPage(LibrarySnapshot snapshot, IReadOnlyList<LibraryEntry> counted) => Totals(snapshot, counted) with
    {
        Items = [.. InMixerOrder(counted).Take(ContractLimits.LibrarySongItemsMax).Select(entry => AssetMapper.ToDto(entry.Asset, snapshot.TrackExists(entry.Asset)))],
    };

    private static LibraryFolderViewDto Totals(LibrarySnapshot snapshot, IReadOnlyList<LibraryEntry> counted) => new()
    {
        Revision = snapshot.Revision,
        ChildrenTotal = 0,
        TotalItems = counted.Count,
        TotalSizeBytes = counted.Sum(entry => entry.Asset.SizeBytes),
        PendingFiling = snapshot.PendingFiling,
    };

    private static List<LibraryEntry> Filter(IEnumerable<LibraryEntry> entries, FolderQuery query) =>
        [.. entries.Where(entry => (query.Kind is null || entry.Asset.Kind == query.Kind) && (query.Status is null || entry.Asset.Status == query.Status))];

    private static FolderCountsDto Counts(FolderLevel level, IReadOnlyList<LibraryEntry> counted) => new()
    {
        Albums = level == FolderLevel.Artist ? PositiveOrNull(counted.Select(entry => entry.AlbumId).Distinct(StringComparer.Ordinal).Count()) : null,
        Songs = level != FolderLevel.Song ? PositiveOrNull(counted.Select(entry => entry.SongId).Distinct(StringComparer.Ordinal).Count()) : null,
        Items = counted.Count,
    };

    private static int? PositiveOrNull(int count) => count > 0 ? count : null;

    private static int? YearOf(IReadOnlyList<LibraryEntry> all) =>
        all.OrderBy(entry => entry.Placement.Album.Source == PlacementSource.Match ? 0 : 1).ThenBy(entry => entry.FiledOrder)
            .Select(entry => entry.Placement.Year).FirstOrDefault(year => year is not null);

    private static IEnumerable<ArtImageDto> Images(LibrarySnapshot snapshot, FolderLevel level, IReadOnlyList<LibraryEntry> all)
    {
        if (level != FolderLevel.Artist)
        {
            return FolderArtPicker.BestImage(snapshot, all) is { } image ? [image] : [];
        }

        var albums = all.GroupBy(entry => entry.AlbumId, StringComparer.Ordinal).Select(group => (Entries: group.ToList(), Folder: AlbumKey(group.ToList())));
        return albums.OrderBy(album => album.Folder.Year is null).ThenBy(album => album.Folder.Year).ThenBy(album => album.Folder.SortName, StringComparer.InvariantCulture)
            .Select(album => FolderArtPicker.BestImage(snapshot, album.Entries)).OfType<ArtImageDto>();
    }

    private static (int? Year, string SortName) AlbumKey(IReadOnlyList<LibraryEntry> entries) =>
        (YearOf(entries), LibraryKey.SortKey(FolderLevel.Album, DisplayName(entries, FolderLevel.Album).Name));

    private static IReadOnlyList<Guid> TrackIdsOf(LibrarySnapshot snapshot, IReadOnlyList<LibraryEntry> all) =>
        [.. all.Select(entry => entry.Asset.TrackId).OfType<Guid>().Where(snapshot.Tracks.ContainsKey).Distinct().Take(ContractLimits.FolderTrackIdsMax)];
}
