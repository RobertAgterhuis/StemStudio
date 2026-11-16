using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Tools;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Policy;

/// <summary>
/// A plan-scoped approval (data-model 4.3): covers later heavy steps of the same run with the same tool and the same
/// single target id, when that tool is an item of the plan. A grant without a target covers nothing (plan scope is per
/// target, so target-less heavy tools always ask).
/// </summary>
/// <param name="ToolName">Tool of the approved step.</param>
/// <param name="TargetId">Target of the approved step (its <c>trackId</c>); null never matches.</param>
public sealed record PlanHeavyGrant(string ToolName, string? TargetId);

/// <summary>Everything the policy needs to decide one call (pure input, data-model 6).</summary>
public sealed record PolicyInput
{
    /// <summary>The tool.</summary>
    public required AgentToolDescriptor Tool { get; init; }

    /// <summary>Validated arguments.</summary>
    public required JsonElement Arguments { get; init; }

    /// <summary>True when the run read external text before this call.</summary>
    public required bool Tainted { get; init; }

    /// <summary>The texts of the conversation's user messages (provenance).</summary>
    public required IReadOnlyList<string> UserMessages { get; init; }

    /// <summary>The run's plan.</summary>
    public IReadOnlyList<PlanItem> Plan { get; init; } = [];

    /// <summary>Plan-scoped approvals granted earlier in the run.</summary>
    public IReadOnlyList<PlanHeavyGrant> PlanGrants { get; init; } = [];

    /// <summary>True for a recorded undo compensation (undo runs).</summary>
    public bool IsCompensation { get; init; }

    /// <summary>True when the compensated artefact is fresh (created by the run and unchanged since).</summary>
    public bool Fresh { get; init; }

    /// <summary>Artist names of the library (artist-imitation guardrail).</summary>
    public IReadOnlyList<string?> LibraryArtists { get; init; } = [];
}

/// <summary>The policy's decision.</summary>
/// <param name="Decision">auto, approval_required or denied.</param>
/// <param name="Reasons">Reason codes (AgentDecisionReasons).</param>
/// <param name="Refusal">Readable refusal for denied calls.</param>
public sealed record PolicyDecision(StepDecisionDto Decision, IReadOnlyList<string> Reasons, string? Refusal)
{
    /// <summary>Execute without asking.</summary>
    public static PolicyDecision Auto { get; } = new(StepDecisionDto.Auto, [], null);

    /// <summary>A guardrail denial.</summary>
    /// <param name="reason">Reason code.</param>
    /// <param name="refusal">Readable refusal.</param>
    /// <returns>The decision.</returns>
    public static PolicyDecision Deny(string reason, string refusal) => new(StepDecisionDto.Denied, [reason], refusal);
}
