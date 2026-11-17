using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.LyricAssist;

/// <summary>The backend and model that answer an assist (shown per suggestion, FR-005).</summary>
public sealed record LyricModelInfo
{
    /// <summary>Backend (<c>ollama</c>, <c>claude</c> or <c>fake</c>).</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Model name, e.g. <c>qwen3.5:9b</c>.</summary>
    public required string Model { get; init; }

    /// <summary>True while the CPU fallback model is used because a GPU job runs.</summary>
    public required bool UsingFallback { get; init; }
}
