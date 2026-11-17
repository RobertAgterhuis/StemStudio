using StemStudio.Contracts.Library;
using StemStudio.Web.Services;

namespace StemStudio.Web.Library;

/// <summary>Readable texts of folders (feature 022, FR-014, US2 scenario 7): counts, levels and tile names.</summary>
public static class FolderText
{
    /// <summary>The level as a word.</summary>
    /// <param name="level">The level.</param>
    /// <returns><c>artist</c>, <c>album</c> or <c>song</c>.</returns>
    public static string Level(FolderLevelDto level) => LibraryFolderRequest.Wire(level);

    /// <summary>The search field under which the API marks a folder's name (its level, as in an item's path).</summary>
    /// <param name="level">The folder's level.</param>
    /// <returns><see cref="SearchFieldDto.Artist"/>, <see cref="SearchFieldDto.Album"/> or <see cref="SearchFieldDto.Song"/>.</returns>
    public static SearchFieldDto SearchField(FolderLevelDto level) => level switch
    {
        FolderLevelDto.Artist => SearchFieldDto.Artist,
        FolderLevelDto.Album => SearchFieldDto.Album,
        _ => SearchFieldDto.Song,
    };

    /// <summary>The level of the children of a folder (artists at the root).</summary>
    /// <param name="folder">The open folder, or null at the root.</param>
    /// <returns>The child level, or null at song level (items).</returns>
    public static FolderLevelDto? ChildLevel(FolderLevelDto? folder) => folder switch
    {
        null => FolderLevelDto.Artist,
        FolderLevelDto.Artist => FolderLevelDto.Album,
        FolderLevelDto.Album => FolderLevelDto.Song,
        _ => null,
    };

    /// <summary>The counts of a folder.</summary>
    /// <param name="counts">The counts.</param>
    /// <returns>For example "3 albums, 12 items" or "1 item".</returns>
    public static string Counts(FolderCountsDto counts)
    {
        var parts = new List<string>(3);
        if (counts.Albums is { } albums)
        {
            parts.Add(Plural(albums, "album"));
        }

        if (counts.Albums is null && counts.Songs is { } songs)
        {
            parts.Add(Plural(songs, "song"));
        }

        parts.Add(Plural(counts.Items, "item"));
        return string.Join(", ", parts);
    }

    /// <summary>Counts and size of a folder header.</summary>
    /// <param name="folder">The folder.</param>
    /// <returns>For example "3 albums, 12 items, 205 MB".</returns>
    public static string Header(LibraryFolderDto folder) => $"{Counts(folder.Counts)}, {ByteSize.Format(folder.SizeBytes)}";

    /// <summary>A count with its noun.</summary>
    /// <param name="count">The count.</param>
    /// <param name="noun">The singular noun.</param>
    /// <returns>"1 item" or "3 items".</returns>
    public static string Plural(int count, string noun) => count == 1 ? $"1 {noun}" : $"{count} {noun}s";
}
