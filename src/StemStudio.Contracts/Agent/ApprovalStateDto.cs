namespace StemStudio.Contracts.Agent;

/// <summary>State of an approval (also the approval.resolved event).</summary>
public sealed record ApprovalStateDto
{
    /// <summary>Step the approval belongs to (same id).</summary>
    public required Guid StepId { get; init; }

    /// <summary>Approval state.</summary>
    public required ApprovalStatusDto Status { get; init; }

    /// <summary>Scope of the decision (once while pending).</summary>
    public required ApprovalScopeDto Scope { get; init; }

    /// <summary>Decision time (UTC).</summary>
    public DateTimeOffset? DecidedAt { get; init; }
}
