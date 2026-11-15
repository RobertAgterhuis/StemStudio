using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Lyrics;

/// <summary>Best timing of a lyrics document.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SyncLevelDto>))]
public enum SyncLevelDto
{
    /// <summary>Word timing from the source or the alignment.</summary>
    [JsonStringEnumMemberName("word")]
    Word,

    /// <summary>Line timing; word times are interpolated.</summary>
    [JsonStringEnumMemberName("line")]
    Line,

    /// <summary>Text only, no times.</summary>
    [JsonStringEnumMemberName("plain")]
    Plain,
}
