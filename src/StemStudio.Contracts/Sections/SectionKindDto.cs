using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>Functional kind of a song section (<c>SectionKind</c>, data-model 2.3) (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionKindDto>))]
public enum SectionKindDto
{
    /// <summary>Before the first vocal part.</summary>
    [JsonStringEnumMemberName("intro")]
    Intro,

    /// <summary>A repeating vocal part that is not the chorus.</summary>
    [JsonStringEnumMemberName("verse")]
    Verse,

    /// <summary>Lead-in to a chorus (set by the user; the analyser does not emit it).</summary>
    [JsonStringEnumMemberName("pre-chorus")]
    PreChorus,

    /// <summary>The repeating vocal part with the most energy.</summary>
    [JsonStringEnumMemberName("chorus")]
    Chorus,

    /// <summary>A vocal part that occurs once after the first chorus.</summary>
    [JsonStringEnumMemberName("bridge")]
    Bridge,

    /// <summary>A low-vocal part led by one instrument (<c>instrument</c> names it).</summary>
    [JsonStringEnumMemberName("solo")]
    Solo,

    /// <summary>Another low-vocal middle part.</summary>
    [JsonStringEnumMemberName("instrumental")]
    Instrumental,

    /// <summary>After the last vocal part.</summary>
    [JsonStringEnumMemberName("outro")]
    Outro,

    /// <summary>Anything else ("Part A" when nothing repeats).</summary>
    [JsonStringEnumMemberName("other")]
    Other,
}
