namespace StemStudio.Contracts.LibraryCare;

/// <summary>A clean-up preview (<c>CleanupPlan</c>); valid for 10 minutes (backend-api 1.12.0, feature 013).</summary>
public sealed record CleanupPlanDto
{
    /// <summary>Plan id for the execution.</summary>
    public required Guid PlanId { get; init; }

    /// <summary>When it was planned.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>When it expires.</summary>
    public required DateTimeOffset ExpiresAt { get; init; }

    /// <summary>What will be removed.</summary>
    public required IReadOnlyList<CleanupItemDto> Items { get; init; }

    /// <summary>What is skipped and why.</summary>
    public required IReadOnlyList<CleanupSkipDto> Skipped { get; init; }

    /// <summary>Bytes freed by the plan.</summary>
    public required long TotalBytes { get; init; }
}
