using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Styles;

/// <summary>Kind of a style (backend contract 1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StyleKindDto>))]
public enum StyleKindDto
{
    /// <summary>Curated and read-only.</summary>
    [JsonStringEnumMemberName("preset")]
    Preset,

    /// <summary>Created by the user.</summary>
    [JsonStringEnumMemberName("user")]
    User,
}
