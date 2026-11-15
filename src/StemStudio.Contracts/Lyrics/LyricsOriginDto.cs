using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Dominant word origin of a lyrics document.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LyricsOriginDto>))]
public enum LyricsOriginDto
{
    /// <summary>From the lyrics source.</summary>
    [JsonStringEnumMemberName("source")]
    Source,

    /// <summary>Estimated from the line timing.</summary>
    [JsonStringEnumMemberName("interpolated")]
    Interpolated,

    /// <summary>From the automatic alignment.</summary>
    [JsonStringEnumMemberName("aligned")]
    Aligned,

    /// <summary>No timing (plain document).</summary>
    [JsonStringEnumMemberName("none")]
    None,
}
