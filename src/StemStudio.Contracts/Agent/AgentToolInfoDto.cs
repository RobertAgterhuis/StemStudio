namespace StemStudio.Contracts.Agent;

/// <summary>Registry view of one tool (GET /api/agent/tools).</summary>
public sealed record AgentToolInfoDto
{
    /// <summary>Tool name from the tool catalogue (snake_case).</summary>
    public required string Name { get; init; }

    /// <summary>Feature that provides the tool, <c>001</c>..<c>011</c>.</summary>
    public required string Feature { get; init; }

    /// <summary>Where the tool runs.</summary>
    public required ToolKindDto Kind { get; init; }

    /// <summary>Risk class.</summary>
    public required ToolRiskDto Risk { get; init; }

    /// <summary>Undo kind.</summary>
    public required UndoKindDto Undo { get; init; }

    /// <summary>True when offered to the model for the given page context.</summary>
    public required bool Available { get; init; }

    /// <summary>Why the tool is not offered, when unavailable.</summary>
    public string? UnavailableReason { get; init; }

    /// <summary>Short readable title.</summary>
    public required string Title { get; init; }

    /// <summary>Description the model sees (English).</summary>
    public required string Description { get; init; }
}
