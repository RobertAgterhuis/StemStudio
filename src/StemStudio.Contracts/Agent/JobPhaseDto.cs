using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Kind of background job a step started or waits for (wait_for_job).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<JobPhaseDto>))]
public enum JobPhaseDto
{
    /// <summary>Standard separation of an import or retry (001).</summary>
    [JsonStringEnumMemberName("standard")]
    Standard,

    /// <summary>Detail separation (002).</summary>
    [JsonStringEnumMemberName("detail")]
    Detail,

    /// <summary>Lyrics search or alignment (006).</summary>
    [JsonStringEnumMemberName("lyrics")]
    Lyrics,

    /// <summary>Audio export or stem pack (007).</summary>
    [JsonStringEnumMemberName("export")]
    Export,

    /// <summary>Sheet music (008).</summary>
    [JsonStringEnumMemberName("transcription")]
    Transcription,

    /// <summary>Style transfer (009, pending 009).</summary>
    [JsonStringEnumMemberName("restyle")]
    Restyle,

    /// <summary>Song generation (010, pending 010).</summary>
    [JsonStringEnumMemberName("generation")]
    Generation,

    /// <summary>Lyric assist run (1.23.0, feature 023); the job id is the assist id.</summary>
    [JsonStringEnumMemberName("lyricAssist")]
    LyricAssist,
}
