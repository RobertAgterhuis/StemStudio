using StemStudio.Contracts.Library;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>
/// Folder orders (feature 022, FR-018): <c>name</c> A-Z with a leading "The" ignored and unknown folders ("Unknown
/// artist", "My songs", ...) last; <c>year</c> with unknown years last; <c>recent</c>, <c>size</c> and <c>items</c>
/// largest first; ties by name. Sorting uses the invariant culture. Pure.
/// </summary>
public static class FolderSorting
{
    /// <summary>The default order of a child level: albums by year, artists and songs by name.</summary>
    /// <param name="childLevel">Level of the listed folders.</param>
    /// <returns>The default sort.</returns>
    public static FolderSortDto DefaultFor(FolderLevel childLevel) => childLevel == FolderLevel.Album ? FolderSortDto.Year : FolderSortDto.Name;

    /// <summary>Sorts folders.</summary>
    /// <param name="folders">Folders of one level.</param>
    /// <param name="level">Their level.</param>
    /// <param name="sort">The order.</param>
    /// <returns>The sorted folders.</returns>
    public static IEnumerable<LibraryFolderDto> Sort(IEnumerable<LibraryFolderDto> folders, FolderLevel level, FolderSortDto sort)
    {
        ArgumentNullException.ThrowIfNull(folders);
        return sort switch
        {
            FolderSortDto.Year => ByName(folders.OrderBy(folder => folder.Year is null).ThenBy(folder => folder.Year), level),
            FolderSortDto.Recent => ByName(folders.OrderByDescending(folder => folder.LatestAt), level),
            FolderSortDto.Size => ByName(folders.OrderByDescending(folder => folder.SizeBytes), level),
            FolderSortDto.Items => ByName(folders.OrderByDescending(folder => folder.Counts.Items), level),
            _ => ByName(folders.OrderBy(_ => 0), level),
        };
    }

    private static IOrderedEnumerable<LibraryFolderDto> ByName(IOrderedEnumerable<LibraryFolderDto> folders, FolderLevel level) => folders
        .ThenBy(folder => folder.NameSource == PlacementSourceDto.Fallback)
        .ThenBy(folder => LibraryKey.SortKey(level, folder.Name), StringComparer.InvariantCulture)
        .ThenBy(folder => folder.Id, StringComparer.Ordinal);
}
