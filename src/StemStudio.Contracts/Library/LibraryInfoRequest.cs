namespace StemStudio.Contracts.Library;

/// <summary>Body of <c>PUT /api/tracks/{trackId}/library-info</c> and <c>PUT /api/assets/{assetId}/library-info</c> (backend-api 1.22.0 <c>LibraryInfoRequest</c>): all three members are required, null = automatic.</summary>
public sealed record LibraryInfoRequest
{
    /// <summary>Artist name (1..200 characters after trimming, no control characters) or null for automatic.</summary>
    public required string? Artist { get; init; }

    /// <summary>Album name or null for automatic.</summary>
    public required string? Album { get; init; }

    /// <summary>Song name or null for automatic.</summary>
    public required string? Song { get; init; }

    /// <summary>All three levels automatic (the body equivalent of <c>DELETE .../library-info</c>).</summary>
    public static LibraryInfoRequest Automatic { get; } = new() { Artist = null, Album = null, Song = null };
}
