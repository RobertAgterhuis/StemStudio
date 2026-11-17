using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Output of one model call (untrusted JSON text).</summary>
public sealed record LyricModelReply
{
    /// <summary>Raw JSON text as returned by the model.</summary>
    public required string Json { get; init; }

    /// <summary>The backend and model that answered.</summary>
    public required LyricModelInfo Model { get; init; }

    /// <summary>Claude token usage of the call; null for local models.</summary>
    public TokenUsageDto? Usage { get; init; }
}
