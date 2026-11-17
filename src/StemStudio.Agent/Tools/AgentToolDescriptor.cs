using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>
/// One tool of the catalogue (data-model 5). Name, kind, risk and undo kind are contract values (tool catalogue 1.0.0);
/// descriptions may be tuned. Server tools carry a <see cref="Handler"/>; UI tools are executed by the open page and
/// optionally mapped by <see cref="MapUiResult"/>; meta tools are executed by the runner.
/// </summary>
public sealed record AgentToolDescriptor
{
    /// <summary>Tool name (snake_case, verb first).</summary>
    public required string Name { get; init; }

    /// <summary>Feature number "001".."011".</summary>
    public required string Feature { get; init; }

    /// <summary>server, ui or meta.</summary>
    public required ToolKindDto Kind { get; init; }

    /// <summary>Risk class (fixed in code, never decided by the model).</summary>
    public required ToolRiskDto Risk { get; init; }

    /// <summary>Undo kind.</summary>
    public required UndoKindDto UndoKind { get; init; }

    /// <summary>Selection group.</summary>
    public required ToolGroup Group { get; init; }

    /// <summary>Availability rule.</summary>
    public required ToolAvailability Availability { get; init; }

    /// <summary>Short human title (tool list, approval cards).</summary>
    public required string Title { get; init; }

    /// <summary>Description the model sees (English).</summary>
    public required string Description { get; init; }

    /// <summary>Argument schema.</summary>
    public required ToolSchema Schema { get; init; }

    /// <summary>Executes a server tool; null for ui and meta tools.</summary>
    public ToolHandler? Handler { get; init; }

    /// <summary>Estimates a heavy step before approval (minutes, device, effect); null when not needed.</summary>
    public ToolEstimator? Estimator { get; init; }

    /// <summary>
    /// True when this call must be treated as <c>heavy</c> (e.g. an export estimated above 500 MB); evaluated before the
    /// policy decides. A failing check keeps the declared risk.
    /// </summary>
    public ToolEscalation? HeavyWhen { get; init; }

    /// <summary>
    /// A check before the policy decides (no approval card, no job when it fails): throws
    /// <see cref="Ports.AgentActionException"/> with a fixed readable message to fail the step.
    /// </summary>
    public ToolPrecondition? Precondition { get; init; }

    /// <summary>Maps the result of a UI tool to the projection and undo action; null projects the result as is.</summary>
    public UiResultMapper? MapUiResult { get; init; }

    /// <summary>Arguments holding a URL that must appear literally in a user message (FR-014).</summary>
    public IReadOnlyList<string> UrlArguments { get; init; } = [];

    /// <summary>Free-text arguments checked by the artist-imitation guardrail (FR-025).</summary>
    public IReadOnlyList<string> ImitationArguments { get; init; } = [];

    /// <summary>Lyrics arguments that must occur in a user message (lyrics-not-from-user).</summary>
    public IReadOnlyList<string> LyricsArguments { get; init; } = [];

    /// <summary>Single words that must occur as a word in a user message (find_rhymes.word, tool catalogue 1.2.0).</summary>
    public IReadOnlyList<string> UserWordArguments { get; init; } = [];

    /// <summary>
    /// A decision reason that always requires approval (e.g. <c>lyrics-edit</c> of apply_lyric_suggestions): never covered by a
    /// plan grant, never skipped as fresh.
    /// </summary>
    public string? AlwaysApprovalReason { get; init; }

    /// <summary>Argument naming the target of the step for plan-scoped approvals (usually trackId).</summary>
    public string? TargetArgument { get; init; }
}
