using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Rhyme quality (<c>Rhyme.kind</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<RhymeKindDto>))]
public enum RhymeKindDto
{
    /// <summary>Equal rhyme part.</summary>
    [JsonStringEnumMemberName("perfect")]
    Perfect,

    /// <summary>Same vowel or same consonants.</summary>
    [JsonStringEnumMemberName("near")]
    Near,
}
