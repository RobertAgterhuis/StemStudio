using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>Who started an assist (<c>LyricAssistSummary.origin</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistOriginDto>))]
public enum AssistOriginDto
{
    /// <summary>The lyrics editor.</summary>
    [JsonStringEnumMemberName("editor")]
    Editor,

    /// <summary>The AI composer agent.</summary>
    [JsonStringEnumMemberName("agent")]
    Agent,
}
