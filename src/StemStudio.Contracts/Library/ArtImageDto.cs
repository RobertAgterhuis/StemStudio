namespace StemStudio.Contracts.Library;

/// <summary>One picture of folder art: the 005 artwork URLs of a track (backend-api 1.22.0 <c>ArtImage</c>).</summary>
public sealed record ArtImageDto
{
    /// <summary><c>/api/tracks/{trackId}/artwork/thumb?v={version}</c> (160 px, immutable).</summary>
    public required string ThumbUrl { get; init; }

    /// <summary><c>/api/tracks/{trackId}/artwork/cover?v={version}</c> (600 px, immutable).</summary>
    public required string CoverUrl { get; init; }
}
