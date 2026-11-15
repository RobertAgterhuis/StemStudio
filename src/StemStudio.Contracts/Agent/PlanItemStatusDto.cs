using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Status of a plan item shown as a checklist.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<PlanItemStatusDto>))]
public enum PlanItemStatusDto
{
    /// <summary>Not started.</summary>
    [JsonStringEnumMemberName("pending")]
    Pending,

    /// <summary>Its step is in progress or waiting.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Its step succeeded.</summary>
    [JsonStringEnumMemberName("done")]
    Done,

    /// <summary>Its step failed.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Not executed (rejected or no longer needed).</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,
}
