using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Kind of a library item (backend contract 1.5.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssetKindDto>))]
public enum AssetKindDto
{
    /// <summary>Saved audio (feature 007).</summary>
    [JsonStringEnumMemberName("audio")]
    Audio,

    /// <summary>Sheet music (reserved for feature 008).</summary>
    [JsonStringEnumMemberName("score")]
    Score,

    /// <summary>A lesson handout PDF (feature 020, backend-api 1.19.0).</summary>
    [JsonStringEnumMemberName("handout")]
    Handout,
}
