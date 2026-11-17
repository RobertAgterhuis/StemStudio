using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Languages of the assistant (schema <c>AssistLanguage</c>, a subset of the 010 lyrics languages).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistLanguageDto>))]
public enum AssistLanguageDto
{
    /// <summary>Dutch.</summary>
    [JsonStringEnumMemberName("nl")]
    Nl,

    /// <summary>English (US or GB spelling).</summary>
    [JsonStringEnumMemberName("en")]
    En,
}
