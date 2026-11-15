namespace StemStudio.Contracts.Assets;

/// <summary>One file of a ready library item.</summary>
/// <param name="Role">File role.</param>
/// <param name="ContentType"><c>audio/flac</c>, <c>audio/wav</c> or <c>audio/mpeg</c>.</param>
/// <param name="SizeBytes">File size in bytes.</param>
/// <param name="Sha256">SHA-256 of the file (64 lower-case hex characters).</param>
/// <param name="Url">Relative URL, see <see cref="ApiRoutes.AssetFile"/>.</param>
public sealed record AssetFileDto(AssetFileRoleDto Role, string ContentType, long SizeBytes, string Sha256, string Url);
