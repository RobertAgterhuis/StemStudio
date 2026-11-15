using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Section markers of lyrics sheets (1.9.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionMarkerDto>))]
public enum SectionMarkerDto
{
    /// <summary>Intro.</summary>
    [JsonStringEnumMemberName("intro")]
    Intro,

    /// <summary>Verse.</summary>
    [JsonStringEnumMemberName("verse")]
    Verse,

    /// <summary>Pre-chorus.</summary>
    [JsonStringEnumMemberName("pre-chorus")]
    PreChorus,

    /// <summary>Chorus.</summary>
    [JsonStringEnumMemberName("chorus")]
    Chorus,

    /// <summary>Bridge.</summary>
    [JsonStringEnumMemberName("bridge")]
    Bridge,

    /// <summary>Outro.</summary>
    [JsonStringEnumMemberName("outro")]
    Outro,

    /// <summary>Instrumental part.</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,
}
