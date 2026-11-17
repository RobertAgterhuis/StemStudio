using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>The orders offered for the children of a level (feature 022, FR-018; Advanced): year only for albums.</summary>
public static class FolderSortOptions
{
    /// <summary>The options of the <c>library-sort</c> select.</summary>
    /// <param name="childLevel">The level of the children, or null at song level (no folders).</param>
    /// <returns>Wire value and label, the default first.</returns>
    public static IReadOnlyList<(string Value, string Label)> For(FolderLevelDto? childLevel)
    {
        if (childLevel is null)
        {
            return [];
        }

        List<(string, string)> common = [("name", "Name A-Z"), ("recent", "Recently added"), ("size", "Size"), ("items", "Number of items")];
        return childLevel == FolderLevelDto.Album ? [("year", "Year"), .. common] : common;
    }
}
