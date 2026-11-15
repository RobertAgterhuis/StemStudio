using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Where a word time comes from.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WordOriginDto>))]
public enum WordOriginDto
{
    /// <summary>From the lyrics source (word-synced record).</summary>
    [JsonStringEnumMemberName("source")]
    Source,

    /// <summary>Estimated from the line timing.</summary>
    [JsonStringEnumMemberName("interpolated")]
    Interpolated,

    /// <summary>From the automatic alignment to the vocal stem.</summary>
    [JsonStringEnumMemberName("aligned")]
    Aligned,
}
