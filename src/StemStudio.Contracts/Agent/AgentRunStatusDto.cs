using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Status of an agent run; completed, failed, cancelled and interrupted are terminal (data-model 4.1).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentRunStatusDto>))]
public enum AgentRunStatusDto
{
    /// <summary>Created, waiting for the run executor.</summary>
    [JsonStringEnumMemberName("queued")]
    Queued,

    /// <summary>A model turn or a tool call is in progress.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Suspended until the user approves or rejects a step.</summary>
    [JsonStringEnumMemberName("awaiting_approval")]
    AwaitingApproval,

    /// <summary>Waiting for the open page to execute a UI tool (15 s).</summary>
    [JsonStringEnumMemberName("awaiting_ui")]
    AwaitingUi,

    /// <summary>Waiting for a background job (no model turn while waiting).</summary>
    [JsonStringEnumMemberName("waiting_job")]
    WaitingJob,

    /// <summary>The model ended without further calls; the answer is stored.</summary>
    [JsonStringEnumMemberName("completed")]
    Completed,

    /// <summary>Stopped by a limit, the backend, a refusal or the budget; see the failure.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>Cancelled by the user.</summary>
    [JsonStringEnumMemberName("cancelled")]
    Cancelled,

    /// <summary>The API restarted while the run was active.</summary>
    [JsonStringEnumMemberName("interrupted")]
    Interrupted,
}
