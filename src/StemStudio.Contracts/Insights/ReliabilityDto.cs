using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Reliability label of a dimension (gate G1, research R-04) (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReliabilityDto>))]
public enum ReliabilityDto
{
    /// <summary>Passed the real-data validation for this stem type.</summary>
    [JsonStringEnumMemberName("validated")]
    Validated,

    /// <summary>Rough indication (not validated).</summary>
    [JsonStringEnumMemberName("indicative")]
    Indicative,
}
