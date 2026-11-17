using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Practice;

/// <summary>Kind snapshot of a practice section row: a <c>SectionKind</c> or <c>none</c> (no section) (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PracticeSectionKindDto>))]
public enum PracticeSectionKindDto
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

    /// <summary>Solo.</summary>
    [JsonStringEnumMemberName("solo")]
    Solo,

    /// <summary>Instrumental.</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,

    /// <summary>Outro.</summary>
    [JsonStringEnumMemberName("outro")]
    Outro,

    /// <summary>Other.</summary>
    [JsonStringEnumMemberName("other")]
    Other,

    /// <summary>Time outside any section (or a track without sections).</summary>
    [JsonStringEnumMemberName("none")]
    None,
}
