namespace StemStudio.Contracts.Tracks;

/// <summary>One separated stem of a track.</summary>
/// <param name="Name">Stem name, see <see cref="StemNames"/>.</param>
/// <param name="Url">Relative URL of the FLAC file, e.g. <c>/api/tracks/{id}/stems/vocals</c>.</param>
public sealed record StemDto(string Name, string Url);
