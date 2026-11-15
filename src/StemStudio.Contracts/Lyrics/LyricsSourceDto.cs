using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Where the lyrics text comes from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LyricsSourceDto>))]
public enum LyricsSourceDto
{
    /// <summary>LRCLIB (credit "Lyrics: LRCLIB").</summary>
    [JsonStringEnumMemberName("lrclib")]
    Lrclib,

    /// <summary>Entered by the user.</summary>
    [JsonStringEnumMemberName("user")]
    User,
}
