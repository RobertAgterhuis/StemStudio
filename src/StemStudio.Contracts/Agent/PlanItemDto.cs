namespace StemStudio.Contracts.Agent;

/// <summary>One item of the plan checklist (set_plan).</summary>
public sealed record PlanItemDto
{
    /// <summary>Position, from 0.</summary>
    public required int Index { get; init; }

    /// <summary>Readable title, at most 120 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Tool the item expects, when known.</summary>
    public string? Tool { get; init; }

    /// <summary>Item status.</summary>
    public required PlanItemStatusDto Status { get; init; }
}
