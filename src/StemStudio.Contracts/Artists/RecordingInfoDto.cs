namespace StemStudio.Contracts.Artists;

/// <summary>The matched recording of a track.</summary>
/// <param name="Title">Recording title.</param>
/// <param name="Album">Album (release group) title, when known.</param>
/// <param name="Year">First release year of the album, when known.</param>
public sealed record RecordingInfoDto(string Title, string? Album = null, int? Year = null);
