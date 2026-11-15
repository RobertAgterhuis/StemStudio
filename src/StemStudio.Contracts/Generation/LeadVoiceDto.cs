using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Generation;

/// <summary>Lead voice of a song (1.9.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LeadVoiceDto>))]
public enum LeadVoiceDto
{
    /// <summary>Female lead vocal.</summary>
    [JsonStringEnumMemberName("female")]
    Female,

    /// <summary>Male lead vocal.</summary>
    [JsonStringEnumMemberName("male")]
    Male,

    /// <summary>Left to the model.</summary>
    [JsonStringEnumMemberName("any")]
    Any,
}
