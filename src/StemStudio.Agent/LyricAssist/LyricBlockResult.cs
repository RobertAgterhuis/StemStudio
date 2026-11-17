using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>What one block produced, after validation and verification.</summary>
public sealed record LyricBlockResult
{
    /// <summary>0-based block index.</summary>
    public required int BlockIndex { get; init; }

    /// <summary>True when the block was skipped (invalid output twice, timeout); a note says why.</summary>
    public bool Skipped { get; init; }

    /// <summary>Validated suggestions of the block.</summary>
    public IReadOnlyList<AssistantSuggestion> Suggestions { get; init; } = [];

    /// <summary>The model's verdicts on the block's dictionary misses.</summary>
    public IReadOnlyList<ModelMissVerdict> Misses { get; init; } = [];

    /// <summary>Items dropped by the assistant (anchor, schema, verification, verbatim); <c>filtered</c> is counted by the backend.</summary>
    public DiscardedCountsDto Discarded { get; init; } = DiscardedCountsDto.None;
}
