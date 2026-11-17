using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>How much the check reports (schema <c>Sensitivity</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SensitivityDto>))]
public enum SensitivityDto
{
    /// <summary>Errors only.</summary>
    [JsonStringEnumMemberName("loose")]
    Loose,

    /// <summary>Errors and improvements (default).</summary>
    [JsonStringEnumMemberName("balanced")]
    Balanced,

    /// <summary>Also recognised poetic licences, as ideas.</summary>
    [JsonStringEnumMemberName("strict")]
    Strict,
}
