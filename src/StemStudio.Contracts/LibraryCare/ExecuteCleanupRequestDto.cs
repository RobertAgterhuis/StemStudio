namespace StemStudio.Contracts.LibraryCare;

/// <summary>Executes a clean-up plan (<c>ExecuteCleanupRequest</c>) (backend-api 1.12.0, feature 013).</summary>
public sealed record ExecuteCleanupRequestDto
{
    /// <summary>The plan from the preview.</summary>
    public required Guid PlanId { get; init; }
}
