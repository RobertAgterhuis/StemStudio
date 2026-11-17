using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>What an assist does (schema <c>AssistKind</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistKindDto>))]
public enum AssistKindDto
{
    /// <summary>Spelling, grammar, flow and licence check.</summary>
    [JsonStringEnumMemberName("check")]
    Check,

    /// <summary>Stylistic devices and proposed poetic licence.</summary>
    [JsonStringEnumMemberName("devices")]
    Devices,

    /// <summary>Alternative versions of one line.</summary>
    [JsonStringEnumMemberName("alternatives")]
    Alternatives,

    /// <summary>Continuation lines after a line.</summary>
    [JsonStringEnumMemberName("continue")]
    Continue,

    /// <summary>Title ideas.</summary>
    [JsonStringEnumMemberName("titles")]
    Titles,
}
