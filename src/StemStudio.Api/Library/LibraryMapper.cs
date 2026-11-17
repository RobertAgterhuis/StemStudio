using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;

namespace StemStudio.Api.Library;

/// <summary>Maps the 022 domain values to the contract DTOs (backend-api 1.22.0).</summary>
public static class LibraryMapper
{
    /// <summary>
    /// The <c>placement</c> member of an item: its stored placement (<c>filed = true</c>), the automatic preview from its own
    /// facts while the backfill has not reached it (<c>filed = false</c>), or null for items that are never filed.
    /// </summary>
    /// <param name="asset">The item.</param>
    /// <returns>The DTO or null.</returns>
    public static PlacementDto? PlacementOf(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        if (!LibraryFilingRules.IsFileable(asset.Provenance))
        {
            return null;
        }

        return asset.IsFiled ? ToDto(asset.Placement!, filed: true) : ToDto(Preview(asset), filed: false);
    }

    /// <summary>Maps a placement.</summary>
    /// <param name="placement">The placement.</param>
    /// <param name="filed">Whether it is the stored, current placement.</param>
    /// <returns>The DTO.</returns>
    public static PlacementDto ToDto(Placement placement, bool filed)
    {
        ArgumentNullException.ThrowIfNull(placement);
        return new PlacementDto
        {
            Artist = Level(placement, FolderLevel.Artist),
            Album = Level(placement, FolderLevel.Album),
            Song = Level(placement, FolderLevel.Song),
            Year = placement.Year,
            Filed = filed,
        };
    }

    /// <summary>Maps a source.</summary>
    /// <param name="source">The source.</param>
    /// <returns>The DTO value.</returns>
    public static PlacementSourceDto ToDto(PlacementSource source) => (PlacementSourceDto)(int)source;

    /// <summary>Maps a level.</summary>
    /// <param name="level">The level.</param>
    /// <returns>The DTO value.</returns>
    public static FolderLevelDto ToDto(FolderLevel level) => (FolderLevelDto)(int)level;

    private static Placement Preview(Asset asset) =>
        PlacementResolver.Resolve(new PlacementInput { Provenance = asset.Provenance, ItemInfo = asset.LibraryInfo, Previous = asset.Placement });

    private static PlacementLevelDto Level(Placement placement, FolderLevel level) => new()
    {
        FolderId = placement.FolderIdOf(level),
        Name = placement.Of(level).Name,
        Source = ToDto(placement.Of(level).Source),
    };
}
