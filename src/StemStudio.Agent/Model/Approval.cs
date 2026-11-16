using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>A confirmation of one step; the first decision wins (data-model 4.3).</summary>
public sealed record Approval
{
    /// <summary>Same as the step identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Run identifier.</summary>
    public required Guid RunId { get; init; }

    /// <summary>Approval state.</summary>
    public required ApprovalStatusDto Status { get; init; }

    /// <summary>Scope of the decision.</summary>
    public required ApprovalScopeDto Scope { get; init; }

    /// <summary>Copy of the step's hash; re-checked before execution.</summary>
    public required string ArgumentsHash { get; init; }

    /// <summary>Request time (UTC).</summary>
    public required DateTimeOffset RequestedAt { get; init; }

    /// <summary>Decision time (UTC).</summary>
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>How long a pending approval stays decidable.</summary>
    public static TimeSpan Lifetime { get; } = TimeSpan.FromHours(24);

    /// <summary>True when the approval is pending and older than <see cref="Lifetime"/> at <paramref name="now"/>.</summary>
    /// <param name="now">Current time.</param>
    /// <returns>True when it must be treated as expired.</returns>
    public bool IsExpiredAt(DateTimeOffset now) => Status == ApprovalStatusDto.Pending && now - RequestedAt >= Lifetime;
}
