namespace StemStudio.Contracts.Assets;

/// <summary>Body of the <c>202</c> answer of <c>POST /api/assets/stem-packs</c>.</summary>
/// <param name="Items">The created items in catalogue order.</param>
public sealed record StemPackDto(IReadOnlyList<AssetDto> Items);
