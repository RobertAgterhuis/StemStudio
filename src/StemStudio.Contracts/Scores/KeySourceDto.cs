using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Scores;

/// <summary>Where the key of a score comes from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<KeySourceDto>))]
public enum KeySourceDto
{
    /// <summary>Estimated from the notes.</summary>
    [JsonStringEnumMemberName("estimated")]
    Estimated,

    /// <summary>Chosen by the user.</summary>
    [JsonStringEnumMemberName("override")]
    Override,
}
