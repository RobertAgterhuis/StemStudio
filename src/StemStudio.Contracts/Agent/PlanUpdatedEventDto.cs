namespace StemStudio.Contracts.Agent;

/// <summary>Data of the plan.updated event.</summary>
public sealed record PlanUpdatedEventDto
{
    /// <summary>The whole plan after the change.</summary>
    public IReadOnlyList<PlanItemDto> Plan { get; init; } = [];
}
