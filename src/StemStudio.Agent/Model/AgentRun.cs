using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>One execution of a user command (data-model 4.1).</summary>
public sealed record AgentRun
{
    /// <summary>Run identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Conversation identifier.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>The command (a synthetic "Undo" message for undo runs).</summary>
    public required Guid UserMessageId { get; init; }

    /// <summary>Run status.</summary>
    public required AgentRunStatusDto Status { get; init; }

    /// <summary>Backend of the run.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Model of the first turn.</summary>
    public required string Model { get; init; }

    /// <summary>True after a tool result with untrusted text; never reset within the run (FR-013).</summary>
    public required bool Tainted { get; init; }

    /// <summary>Plan checklist.</summary>
    public IReadOnlyList<PlanItem> Plan { get; init; } = [];

    /// <summary>Model turns so far.</summary>
    public required int ModelTurns { get; init; }

    /// <summary>Tool calls so far.</summary>
    public required int ToolCalls { get; init; }

    /// <summary>Unknown or invalid calls so far.</summary>
    public required int InvalidCalls { get; init; }

    /// <summary>Summed token usage.</summary>
    public TokenUsage Usage { get; init; } = TokenUsage.Zero;

    /// <summary>Failure code of a failed run, see AgentFailureCodes.</summary>
    public string? FailureCode { get; init; }

    /// <summary>Readable failure, at most 500 characters.</summary>
    public string? FailureMessage { get; init; }

    /// <summary>Start (UTC).</summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>End (UTC) of a terminal run.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>True when the run reached a final state.</summary>
    public bool IsTerminal => AgentStateRules.IsTerminal(Status);
}
