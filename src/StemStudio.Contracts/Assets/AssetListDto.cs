namespace StemStudio.Contracts.Assets;

/// <summary>Body of <c>GET /api/assets</c>: one page of items, newest first.</summary>
/// <param name="Items">The page.</param>
/// <param name="Total">Number of items matching the filters.</param>
/// <param name="TotalSizeBytes">Total size of the items matching the filters.</param>
public sealed record AssetListDto(IReadOnlyList<AssetDto> Items, int Total, long TotalSizeBytes);
