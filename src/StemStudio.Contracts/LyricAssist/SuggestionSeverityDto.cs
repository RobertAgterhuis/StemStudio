using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Severity of a suggestion (schema <c>SuggestionSeverity</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SuggestionSeverityDto>))]
public enum SuggestionSeverityDto
{
    /// <summary>A mistake that is not plausible as deliberate.</summary>
    [JsonStringEnumMemberName("error")]
    Error,

    /// <summary>A possible improvement.</summary>
    [JsonStringEnumMemberName("improvement")]
    Improvement,

    /// <summary>An idea, licence note, device or writing help.</summary>
    [JsonStringEnumMemberName("idea")]
    Idea,
}
