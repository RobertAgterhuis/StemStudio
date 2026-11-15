using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Agent;

/// <summary>Status of one tool call of a run (data-model 4.2).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AgentStepStatusDto>))]
public enum AgentStepStatusDto
{
    /// <summary>Created, not yet decided or executed.</summary>
    [JsonStringEnumMemberName("planned")]
    Planned,

    /// <summary>Waiting for the user's decision.</summary>
    [JsonStringEnumMemberName("awaiting_approval")]
    AwaitingApproval,

    /// <summary>Being executed.</summary>
    [JsonStringEnumMemberName("running")]
    Running,

    /// <summary>Sent to the open page.</summary>
    [JsonStringEnumMemberName("awaiting_ui")]
    AwaitingUi,

    /// <summary>Waiting for the job it started or watches.</summary>
    [JsonStringEnumMemberName("waiting_job")]
    WaitingJob,

    /// <summary>Executed; see the result summary.</summary>
    [JsonStringEnumMemberName("succeeded")]
    Succeeded,

    /// <summary>Executed with an error; see the error.</summary>
    [JsonStringEnumMemberName("failed")]
    Failed,

    /// <summary>The user rejected the step, or the policy denied it.</summary>
    [JsonStringEnumMemberName("rejected")]
    Rejected,

    /// <summary>Unknown tool or invalid arguments; never executed.</summary>
    [JsonStringEnumMemberName("rejected_invalid")]
    RejectedInvalid,

    /// <summary>Not executed because an earlier step was rejected or failed.</summary>
    [JsonStringEnumMemberName("skipped")]
    Skipped,

    /// <summary>The approval expired (run ended, 24 h, API restart).</summary>
    [JsonStringEnumMemberName("expired")]
    Expired,

    /// <summary>Reversed by an undo run.</summary>
    [JsonStringEnumMemberName("undone")]
    Undone,
}
