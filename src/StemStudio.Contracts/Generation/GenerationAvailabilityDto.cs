using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>Availability of the generator or singer service (1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<GenerationAvailabilityDto>))]
public enum GenerationAvailabilityDto
{
    /// <summary>Not configured, or not reachable within 2 s.</summary>
    [JsonStringEnumMemberName("notInstalled")]
    NotInstalled,

    /// <summary>Running without a usable GPU (reason set).</summary>
    [JsonStringEnumMemberName("unavailable")]
    Unavailable,

    /// <summary>Generation is possible.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,
}
