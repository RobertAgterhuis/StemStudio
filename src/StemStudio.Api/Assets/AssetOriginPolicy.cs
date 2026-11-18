using StemStudio.Domain.Assets;

namespace StemStudio.Api.Assets;

/// <summary>Which library endpoints may change an item, by its origin (feature 015: the Takes panel owns recorded takes).</summary>
public static class AssetOriginPolicy
{
    /// <summary>409 <c>managed-by-take</c> for a take's item (rename, delete and retry through the library), else null.</summary>
    /// <param name="asset">The item.</param>
    /// <returns>The refusal or null.</returns>
    public static IResult? ManagedElsewhere(Asset asset)
    {
        ArgumentNullException.ThrowIfNull(asset);
        return asset.Provenance.Origin == AssetOrigin.Recording ? AssetProblems.ManagedByTake() : null;
    }
}
