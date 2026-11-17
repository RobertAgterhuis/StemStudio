using System.Text.Json.Serialization;

namespace StemStudio.Contracts.LyricAssist;

/// <summary>State of an assist (schema <c>AssistStatus</c>).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AssistStatusDto>))]
public enum AssistStatusDto
{
    /// <summary>Created, not started.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>Running.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Finished.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>Failed (also: interrupted by a restart).</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled by the user or because the sheet was deleted.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,
}
