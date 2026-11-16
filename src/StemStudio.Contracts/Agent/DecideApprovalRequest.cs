namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/runs/{runId}/approvals/{stepId}.</summary>
public sealed record DecideApprovalRequest
{
    /// <summary>Approve or reject.</summary>
    public required ApprovalDecisionDto Decision { get; init; }

    /// <summary>Scope of an approval (default once; plan_heavy only when the card allows it).</summary>
    public ApprovalScopeDto? Scope { get; init; }

    /// <summary>Hash shown on the card; a mismatch returns 409 arguments-changed.</summary>
    public required string ArgumentsHash { get; init; }
}
