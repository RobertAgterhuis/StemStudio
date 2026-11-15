using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>One tool call of a run (data-model 4.2).</summary>
public sealed record RunStep
{
    /// <summary>Step identifier; also the approval and UI round-trip id.</summary>
    public required Guid Id { get; init; }

    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Position in the run from 1.</summary>
    public required int Seq { get; init; }

    /// <summary>The model's FunctionCallContent.CallId (at most 100 characters).</summary>
    public required string CallId { get; init; }

    /// <summary>Tool name (unknown names get status rejected_invalid).</summary>
    public required string Tool { get; init; }

    /// <summary>Copied from the descriptor.</summary>
    public required ToolKindDto Kind { get; init; }

    /// <summary>Copied from the descriptor.</summary>
    public required ToolRiskDto Risk { get; init; }

    /// <summary>Copied from the descriptor.</summary>
    public required UndoKindDto UndoKind { get; init; }

    /// <summary>Canonical JSON (sorted keys, no whitespace), at most 8 KB.</summary>
    public required string Arguments { get; init; }

    /// <summary>SHA-256 hex of the canonical arguments (approval binding, FR-015).</summary>
    public required string ArgumentsHash { get; init; }

    /// <summary>Linked plan item.</summary>
    public int? PlanIndex { get; init; }

    /// <summary>Policy decision.</summary>
    public required StepDecisionDto Decision { get; init; }

    /// <summary>Reason codes, see AgentDecisionReasons.</summary>
    public IReadOnlyList<string> DecisionReasons { get; init; } = [];

    /// <summary>Step status.</summary>
    public required AgentStepStatusDto Status { get; init; }

    /// <summary>When execution started (UTC); null while planned or awaiting approval.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>When execution ended (UTC); null while not finished.</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>Estimated job duration in minutes, shown on heavy approval cards.</summary>
    public double? EstimatedMinutes { get; init; }

    /// <summary>JSON projection the model saw, at most 4 KB.</summary>
    public string? ResultSummary { get; init; }

    /// <summary>Readable error, at most 500 characters.</summary>
    public string? Error { get; init; }

    /// <summary>Latest job progress 0-100 from polling.</summary>
    public int? Progress { get; init; }

    /// <summary>The job this step started or waits for.</summary>
    public JobRef? Job { get; init; }
}
