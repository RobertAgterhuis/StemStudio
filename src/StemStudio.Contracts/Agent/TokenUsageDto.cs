namespace StemStudio.Contracts.Agent;

/// <summary>Token usage of a run (summed over turns; Claude usage is shown at Advanced and Expert).</summary>
public sealed record TokenUsageDto
{
    /// <summary>Input tokens.</summary>
    public required long InputTokens { get; init; }

    /// <summary>Output tokens.</summary>
    public required long OutputTokens { get; init; }

    /// <summary>Input tokens read from the prompt cache.</summary>
    public required long CachedInputTokens { get; init; }
}
