using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>A folder of the search index: its display name folded once, its items and its parents.</summary>
/// <param name="Id">Folder id.</param>
/// <param name="Level">Level.</param>
/// <param name="Name">Display name.</param>
/// <param name="Folded">Folded display name (with the index map for highlights).</param>
/// <param name="Entries">Its items.</param>
/// <param name="AncestorIds">Parent folder ids from the artist down.</param>
public sealed record FolderNode(string Id, FolderLevel Level, string Name, FoldedText Folded, IReadOnlyList<LibraryEntry> Entries, IReadOnlyList<string> AncestorIds)
{
    /// <summary>Sort text of the name (a leading "The" ignored for artists and albums).</summary>
    public string SortName { get; } = LibraryKey.SortKey(Level, Name);
}

/// <summary>An item of the search index with its folded name and artist credit.</summary>
/// <param name="Entry">The item.</param>
/// <param name="Name">Folded item name.</param>
/// <param name="Credit">Folded full artist credit of its track (provenance snapshot), or empty.</param>
public sealed record ItemNode(LibraryEntry Entry, FoldedText Name, string Credit);

/// <summary>
/// The folded search fields of a snapshot (feature 022, research R-07), built once per snapshot on the first search:
/// folder display names and item names are folded with the index map, so a search only scans strings.
/// </summary>
public sealed class SearchIndex
{
    private readonly Dictionary<string, FolderNode> _folders;

    private SearchIndex(Dictionary<string, FolderNode> folders, IReadOnlyList<ItemNode> items) => (_folders, Items) = (folders, items);

    /// <summary>Every item.</summary>
    public IReadOnlyList<ItemNode> Items { get; }

    /// <summary>Every folder.</summary>
    public IEnumerable<FolderNode> Folders => _folders.Values;

    /// <summary>Builds the index of a snapshot.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The index.</returns>
    public static SearchIndex Build(LibrarySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var folders = new Dictionary<string, FolderNode>(StringComparer.Ordinal);
        foreach (var entry in snapshot.Entries)
        {
            AddFolders(snapshot, folders, entry);
        }

        var items = snapshot.Entries.Select(entry => new ItemNode(entry, LibraryKey.Fold(entry.Asset.Name), LibraryKey.Fold(entry.Asset.Provenance.TrackArtist ?? string.Empty).Value));
        return new SearchIndex(folders, [.. items]);
    }

    /// <summary>A folder by id.</summary>
    /// <param name="id">Folder id.</param>
    /// <returns>The folder or null.</returns>
    public FolderNode? Folder(string id) => _folders.GetValueOrDefault(id);

    private static void AddFolders(LibrarySnapshot snapshot, Dictionary<string, FolderNode> folders, LibraryEntry entry)
    {
        string[] ids = [entry.ArtistId, entry.AlbumId, entry.SongId];
        for (var level = FolderLevel.Artist; level <= FolderLevel.Song; level++)
        {
            var id = ids[(int)level];
            if (!folders.ContainsKey(id))
            {
                var all = snapshot.In(id);
                var name = FolderTreeBuilder.DisplayName(all, level).Name;
                folders[id] = new FolderNode(id, level, name, LibraryKey.Fold(name), all, ids[..(int)level]);
            }
        }
    }
}
