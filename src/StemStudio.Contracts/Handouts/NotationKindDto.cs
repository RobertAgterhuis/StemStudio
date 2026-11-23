using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Kind of a notation item (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<NotationKindDto>))]
public enum NotationKindDto
{
    /// <summary>Standard score.</summary>
    [JsonStringEnumMemberName("score")]
    Score,

    /// <summary>Tablature.</summary>
    [JsonStringEnumMemberName("tab")]
    Tab,

    /// <summary>Drum notation.</summary>
    [JsonStringEnumMemberName("drums")]
    Drums,

    /// <summary>Transposed part.</summary>
    [JsonStringEnumMemberName("transposed")]
    Transposed,
}
