using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Level of one quality dimension (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QualityLevelDto>))]
public enum QualityLevelDto
{
    /// <summary>Little bleed or few artefacts.</summary>
    [JsonStringEnumMemberName("low")]
    Low,

    /// <summary>Some.</summary>
    [JsonStringEnumMemberName("medium")]
    Medium,

    /// <summary>Much.</summary>
    [JsonStringEnumMemberName("high")]
    High,

    /// <summary>Not shown: the dimension failed validation (G1) or too little audio.</summary>
    [JsonStringEnumMemberName("unknown")]
    Unknown,
}
