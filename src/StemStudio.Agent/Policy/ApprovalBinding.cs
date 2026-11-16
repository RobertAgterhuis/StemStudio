using StemStudio.Agent.Model;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Policy;

/// <summary>Binding of an approval to the exact call it was shown for (FR-015).</summary>
public static class ApprovalBinding
{
    /// <summary>True when the approval was given and its hash equals the hash of the call about to run.</summary>
    /// <param name="approval">The stored approval.</param>
    /// <param name="argumentsHash">Hash of the canonical arguments to execute.</param>
    /// <returns>True when execution is allowed.</returns>
    public static bool Matches(Approval approval, string argumentsHash)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return approval.Status == ApprovalStatusDto.Approved && string.Equals(approval.ArgumentsHash, argumentsHash, StringComparison.Ordinal);
    }

    /// <summary>The plan-scoped grant of an approved <c>plan_heavy</c> decision, or null.</summary>
    /// <param name="approval">The decided approval.</param>
    /// <param name="targetId">Target of the approved step.</param>
    /// <returns>The grant.</returns>
    public static PlanHeavyGrant? Grant(Approval approval, string? targetId)
    {
        ArgumentNullException.ThrowIfNull(approval);
        return approval is { Status: ApprovalStatusDto.Approved, Scope: ApprovalScopeDto.PlanHeavy } ? new PlanHeavyGrant(targetId) : null;
    }
}
