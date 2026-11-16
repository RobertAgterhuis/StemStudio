using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Styles;

/// <summary>Tempo feel of a style (1.8.0).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TempoFeelDto>))]
public enum TempoFeelDto
{
    /// <summary>Follow the source.</summary>
    [JsonStringEnumMemberName("keep")]
    Keep,

    /// <summary>Laid back.</summary>
    [JsonStringEnumMemberName("laidback")]
    Laidback,

    /// <summary>Driving.</summary>
    [JsonStringEnumMemberName("driving")]
    Driving,

    /// <summary>Half time.</summary>
    [JsonStringEnumMemberName("halfTime")]
    HalfTime,

    /// <summary>Swing.</summary>
    [JsonStringEnumMemberName("swing")]
    Swing,
}
