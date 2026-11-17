using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LibraryCare;

/// <summary>Kind of a queued or running job that blocks a restore (<c>ActiveWork.kind</c>) (backend-api 1.12.0, feature 013).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ActiveWorkKindDto>))]
public enum ActiveWorkKindDto
{
    /// <summary>A track import or separation.</summary>
    [JsonStringEnumMemberName("import")]
    Import,

    /// <summary>A detail separation.</summary>
    [JsonStringEnumMemberName("detail")]
    Detail,

    /// <summary>An export or stem pack.</summary>
    [JsonStringEnumMemberName("export")]
    Export,

    /// <summary>A sheet music transcription.</summary>
    [JsonStringEnumMemberName("score")]
    Score,

    /// <summary>A lyrics alignment.</summary>
    [JsonStringEnumMemberName("lyricsAlignment")]
    LyricsAlignment,

    /// <summary>A restyle, song or voice conversion.</summary>
    [JsonStringEnumMemberName("generation")]
    Generation,

    /// <summary>An agent run.</summary>
    [JsonStringEnumMemberName("agentRun")]
    AgentRun,
}
