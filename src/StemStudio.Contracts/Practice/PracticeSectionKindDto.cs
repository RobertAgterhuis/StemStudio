using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Practice;

/// <summary>Kind snapshot of a practice section row: a <c>SectionKind</c> or <c>none</c> (no section) (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PracticeSectionKindDto>))]
public enum PracticeSectionKindDto
{
    /// <summary>Time practised in an intro section.</summary>
    [JsonStringEnumMemberName("intro")]
    Intro,

    /// <summary>Time practised in a verse.</summary>
    [JsonStringEnumMemberName("verse")]
    Verse,

    /// <summary>Time practised in a pre-chorus.</summary>
    [JsonStringEnumMemberName("pre-chorus")]
    PreChorus,

    /// <summary>Time practised in a chorus.</summary>
    [JsonStringEnumMemberName("chorus")]
    Chorus,

    /// <summary>Time practised in a bridge.</summary>
    [JsonStringEnumMemberName("bridge")]
    Bridge,

    /// <summary>Time practised in a solo (any instrument).</summary>
    [JsonStringEnumMemberName("solo")]
    Solo,

    /// <summary>Time practised in an instrumental part.</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,

    /// <summary>Time practised in an outro.</summary>
    [JsonStringEnumMemberName("outro")]
    Outro,

    /// <summary>Time practised in another part ("Part A").</summary>
    [JsonStringEnumMemberName("other")]
    Other,

    /// <summary>Time outside any section (or a track without sections).</summary>
    [JsonStringEnumMemberName("none")]
    None,
}
