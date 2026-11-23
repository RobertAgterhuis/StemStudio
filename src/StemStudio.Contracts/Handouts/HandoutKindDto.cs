using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Handouts;

/// <summary>Whether a handout is for a song or a setlist (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HandoutKindDto>))]
public enum HandoutKindDto
{
    /// <summary>One song.</summary>
    [JsonStringEnumMemberName("song")]
    Song,

    /// <summary>A setlist.</summary>
    [JsonStringEnumMemberName("setlist")]
    Setlist,
}
