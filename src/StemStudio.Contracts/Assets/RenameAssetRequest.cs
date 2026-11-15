namespace StemStudio.Contracts.Assets;

/// <summary>Body of <c>PATCH /api/assets/{id}</c>.</summary>
/// <param name="Name">New name, 1..120 characters after trimming, no control characters.</param>
public sealed record RenameAssetRequest(string Name);
