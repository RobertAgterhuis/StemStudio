using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>One item of a run's plan (set_plan).</summary>
public sealed record PlanItem
{
    /// <summary>Position from 0.</summary>
    public required int Index { get; init; }

    /// <summary>Title, at most 120 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Expected tool, when known.</summary>
    public string? Tool { get; init; }

    /// <summary>Item status.</summary>
    public required PlanItemStatusDto Status { get; init; }
}
