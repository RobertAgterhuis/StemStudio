using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Quality of a piano score.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreQualityDto>))]
public enum ScoreQualityDto
{
    /// <summary>The piano model (GPU when available).</summary>
    [JsonStringEnumMemberName("accurate")]
    Accurate,

    /// <summary>basic-pitch (seconds on the CPU).</summary>
    [JsonStringEnumMemberName("fast")]
    Fast,
}
