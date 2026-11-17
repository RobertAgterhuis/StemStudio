using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Scope of an intentional mark (schema <c>IntentionalScope</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<IntentionalScopeDto>))]
public enum IntentionalScopeDto
{
    /// <summary>One word (case-sensitive).</summary>
    [JsonStringEnumMemberName("word")]
    Word,

    /// <summary>A phrase (exact).</summary>
    [JsonStringEnumMemberName("phrase")]
    Phrase,

    /// <summary>A whole line (exact).</summary>
    [JsonStringEnumMemberName("line")]
    Line,
}
