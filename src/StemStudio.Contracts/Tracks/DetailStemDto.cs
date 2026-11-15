namespace StemStudio.Contracts.Tracks;

/// <summary>One detail stem of a split parent.</summary>
/// <param name="Name">Detail stem name, see <see cref="DetailNames"/>.</param>
/// <param name="Url">Relative URL of the FLAC file, e.g. <c>/api/tracks/{id}/detail-stems/drums/kick</c>.</param>
public sealed record DetailStemDto(string Name, string Url);
