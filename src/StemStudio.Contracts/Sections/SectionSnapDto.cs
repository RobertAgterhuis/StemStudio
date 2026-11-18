using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>What the detected boundaries were snapped to (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionSnapDto>))]
public enum SectionSnapDto
{
    /// <summary>Bars of a reliable beat grid.</summary>
    [JsonStringEnumMemberName("downbeat")]
    Downbeat,

    /// <summary>Beats (unreliable grid).</summary>
    [JsonStringEnumMemberName("beat")]
    Beat,

    /// <summary>0.1 s (no grid).</summary>
    [JsonStringEnumMemberName("time")]
    Time,
}
