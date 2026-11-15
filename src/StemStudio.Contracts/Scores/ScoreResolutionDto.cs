using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Smallest note value of the quantisation grid.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ScoreResolutionDto>))]
public enum ScoreResolutionDto
{
    /// <summary>Sixteenth notes.</summary>
    [JsonStringEnumMemberName("sixteenth")]
    Sixteenth,

    /// <summary>Eighth notes (shown as "simplified").</summary>
    [JsonStringEnumMemberName("eighth")]
    Eighth,
}
