using System.Text.Json;

namespace StemStudio.Contracts.Agent;

/// <summary>One tool call of a run (also the step.created event).</summary>
public sealed record AgentStepDto
{
    /// <summary>Step identifier (also the approval and UI round-trip id).</summary>
    public required Guid StepId { get; init; }

    /// <summary>Position in the run, from 1.</summary>
    public required int Seq { get; init; }

    /// <summary>Tool name (unknown names are shown with status rejected_invalid).</summary>
    public required string Tool { get; init; }

    /// <summary>Where the tool runs.</summary>
    public required ToolKindDto Kind { get; init; }

    /// <summary>Risk class.</summary>
    public required ToolRiskDto Risk { get; init; }

    /// <summary>Undo kind.</summary>
    public required UndoKindDto Undo { get; init; }

    /// <summary>Canonical arguments (at most 8 KB).</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>SHA-256 hex of the canonical arguments.</summary>
    public required string ArgumentsHash { get; init; }

    /// <summary>Linked plan item.</summary>
    public int? PlanIndex { get; init; }

    /// <summary>Policy decision.</summary>
    public required StepDecisionDto Decision { get; init; }

    /// <summary>Reason codes, see <see cref="AgentDecisionReasons"/>.</summary>
    public IReadOnlyList<string> DecisionReasons { get; init; } = [];

    /// <summary>Step status.</summary>
    public required AgentStepStatusDto Status { get; init; }

    /// <summary>Projection the model saw (at most 4 KB).</summary>
    public JsonElement? ResultSummary { get; init; }

    /// <summary>Readable error, at most 500 characters.</summary>
    public string? Error { get; init; }

    /// <summary>Job started or watched by the step.</summary>
    public JobRefDto? Job { get; init; }

    /// <summary>Job progress 0-100.</summary>
    public int? Progress { get; init; }

    /// <summary>Estimate for heavy jobs.</summary>
    public double? EstimatedMinutes { get; init; }

    /// <summary>Execution start (UTC).</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>Execution end (UTC).</summary>
    public DateTimeOffset? EndedAt { get; init; }
}
