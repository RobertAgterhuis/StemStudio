using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Insights;

/// <summary>Status of a track's stem quality report (backend-api 1.19.0, feature 020).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<QualityReportStatusDto>))]
public enum QualityReportStatusDto
{
    /// <summary>Never made.</summary>
    [JsonStringEnumMemberName("none")]
    None,

    /// <summary>Waiting on the worker's insights lane.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Being made.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Ready.</summary>
    [JsonStringEnumMemberName("ready")]
    Ready,

    /// <summary>The last run failed; the last ready stems are kept.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,
}
