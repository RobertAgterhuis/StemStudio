namespace StemStudio.Contracts.Tracks;

/// <summary>The picture shown for a track (backend contract 1.4.0).</summary>
/// <param name="Source">Where the picture came from.</param>
/// <param name="Version">First 12 hex characters of the cover's SHA-256; changes with the picture.</param>
/// <param name="CoverUrl">600 px cover, <c>/api/tracks/{id}/artwork/cover?v={version}</c> (see <see cref="ApiRoutes.TrackArtwork"/>).</param>
/// <param name="ThumbUrl">160 px thumbnail, <c>/api/tracks/{id}/artwork/thumb?v={version}</c>.</param>
public sealed record ArtworkDto(ArtworkSourceDto Source, string Version, string CoverUrl, string ThumbUrl);
