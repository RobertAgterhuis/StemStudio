using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>Links to library items from other screens (feature 022, FR-012, ui-test-ids 1.22.0 <c>export-saved-open</c>).</summary>
public static class LibraryLinks
{
    /// <summary>The link of an item: its row in its song folder, or the 007 highlight link when the item is not placed.</summary>
    /// <param name="assetId">The item.</param>
    /// <param name="placement">Where it is filed, or null.</param>
    /// <returns><c>library/{songFolderId}#asset-{id}</c>, else <c>library?asset={id}</c>.</returns>
    public static string Item(Guid assetId, PlacementDto? placement) =>
        placement is null ? $"{LibraryLocation.RootPath}?asset={assetId}" : LibraryLocation.ItemUrl(placement.Song.FolderId, assetId);
}
