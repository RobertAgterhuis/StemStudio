using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>AI voice label of a take or generated track (1.9.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VocalLabelDto>))]
public enum VocalLabelDto
{
    /// <summary>No vocals (instrumental).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>An AI voice from the song model.</summary>
    [JsonStringEnumMemberName("synthetic")]
    Synthetic,

    /// <summary>Converted to a voice profile.</summary>
    [JsonStringEnumMemberName("converted")]
    Converted,
}
