using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Tracks;

/// <summary>Where the shown picture of a track came from (backend contract 1.4.0). Serialized as lower-case strings.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ArtworkSourceDto>))]
public enum ArtworkSourceDto
{
    /// <summary>A picture chosen by the user (always wins).</summary>
    [JsonStringEnumMemberName("user")]
    User,

    /// <summary>The Spotify album image.</summary>
    [JsonStringEnumMemberName("spotify")]
    Spotify,

    /// <summary>The cover embedded in the uploaded file.</summary>
    [JsonStringEnumMemberName("embedded")]
    Embedded,

    /// <summary>The release group front from the Cover Art Archive.</summary>
    [JsonStringEnumMemberName("coverartarchive")]
    CoverArtArchive,

    /// <summary>The YouTube video thumbnail.</summary>
    [JsonStringEnumMemberName("youtube")]
    Youtube,
}
