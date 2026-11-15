using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Styles;

/// <summary>How far a take may move from the source (1.8.0; cover strength 0.85 / 0.70 / 0.55 / 0.40 / 0.25).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<StrengthDto>))]
public enum StrengthDto
{
    /// <summary>Closest to the source (0.85).</summary>
    [JsonStringEnumMemberName("subtle")]
    Subtle,

    /// <summary>0.70.</summary>
    [JsonStringEnumMemberName("light")]
    Light,

    /// <summary>0.55 (default of most presets).</summary>
    [JsonStringEnumMemberName("balanced")]
    Balanced,

    /// <summary>0.40.</summary>
    [JsonStringEnumMemberName("strong")]
    Strong,

    /// <summary>Furthest from the source (0.25).</summary>
    [JsonStringEnumMemberName("free")]
    Free,
}
