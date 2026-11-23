using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Transposition of a melodic part (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TranspositionDto>))]
public enum TranspositionDto
{
    /// <summary>Concert pitch (C instruments).</summary>
    [JsonStringEnumMemberName("c")]
    C,

    /// <summary>B-flat instruments (written a major second higher).</summary>
    [JsonStringEnumMemberName("b-flat")]
    BFlat,

    /// <summary>E-flat instruments (written a major sixth higher).</summary>
    [JsonStringEnumMemberName("e-flat")]
    EFlat,

    /// <summary>F instruments (written a perfect fifth higher).</summary>
    [JsonStringEnumMemberName("f")]
    F,
}
