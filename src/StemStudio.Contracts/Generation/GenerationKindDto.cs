using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>Kind of a generation (1.8.0: restyle; 1.9.0: song, voiceConversion).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenerationKindDto>))]
public enum GenerationKindDto
{
    /// <summary>A track restyled with a style (feature 009).</summary>
    [JsonStringEnumMemberName("restyle")]
    Restyle,

    /// <summary>A song from own lyrics (feature 010).</summary>
    [JsonStringEnumMemberName("song")]
    Song,

    /// <summary>A song take converted to a voice profile (feature 010).</summary>
    [JsonStringEnumMemberName("voiceConversion")]
    VoiceConversion,
}
