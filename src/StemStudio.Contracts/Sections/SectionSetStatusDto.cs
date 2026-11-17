using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Sections;

/// <summary>Detection state of a track's sections (<c>SectionSetStatus</c>) (backend-api 1.13.0, feature 014).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<SectionSetStatusDto>))]
public enum SectionSetStatusDto
{
    /// <summary>Never detected (the mixer's first open asks for a detection).</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>A section job is queued.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>The worker analyses the stems.</summary>
    [JsonStringEnumMemberName("detecting")]
    Detecting,

    /// <summary>Sections are stored.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The last detection failed; <c>error</c> says why.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
