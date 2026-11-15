namespace StemStudio.Agent.Model;

/// <summary>Token usage, summed from the backend's usage reports.</summary>
/// <param name="InputTokens">Input tokens.</param>
/// <param name="OutputTokens">Output tokens.</param>
/// <param name="CachedInputTokens">Input tokens read from the prompt cache.</param>
public sealed record TokenUsage(long InputTokens, long OutputTokens, long CachedInputTokens)
{
    /// <summary>No usage.</summary>
    public static TokenUsage Zero { get; } = new(0, 0, 0);

    /// <summary>The sum of this usage and another.</summary>
    /// <param name="other">Usage to add.</param>
    /// <returns>A new usage.</returns>
    public TokenUsage Add(TokenUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new(InputTokens + other.InputTokens, OutputTokens + other.OutputTokens, CachedInputTokens + other.CachedInputTokens);
    }
}
