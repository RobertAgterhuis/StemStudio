using System.Text.Json;

namespace StemStudio.Contracts.Agent;

/// <summary>A pending confirmation card (also the approval.requested event).</summary>
public sealed record ApprovalRequestDto
{
    /// <summary>Step to approve.</summary>
    public required Guid StepId { get; init; }

    /// <summary>Tool name.</summary>
    public required string Tool { get; init; }

    /// <summary>Risk class.</summary>
    public required ToolRiskDto Risk { get; init; }

    /// <summary>Exact (canonical) arguments that will be executed.</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>SHA-256 hex of the canonical arguments; must be echoed by the decision.</summary>
    public required string ArgumentsHash { get; init; }

    /// <summary>Why approval is needed, see <see cref="AgentDecisionReasons"/>.</summary>
    public IReadOnlyList<string> Reasons { get; init; } = [];

    /// <summary>Readable effect, e.g. "Deletes the mix Sandman bass".</summary>
    public required string Effect { get; init; }

    /// <summary>True when undo can reverse the step.</summary>
    public required bool Undoable { get; init; }

    /// <summary>Estimated duration of heavy jobs.</summary>
    public double? EstimatedMinutes { get; init; }

    /// <summary>Processing device code of a heavy job: <c>cpu</c> or <c>gpu</c>; null when not heavy or unknown (never a readable device name).</summary>
    public string? Device { get; init; }

    /// <summary>True when the card may offer "approve for this plan".</summary>
    public required bool AllowPlanScope { get; init; }
}
