namespace StemStudio.Contracts.Agent;

/// <summary>Result of a connection test.</summary>
public sealed record BackendTestResultDto
{
    /// <summary>Tested backend.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>True when the backend answered as expected.</summary>
    public required bool Ok { get; init; }

    /// <summary>Round trip in milliseconds.</summary>
    public required int LatencyMs { get; init; }

    /// <summary>Readable result, e.g. the Ollama version or the Claude error (never the key).</summary>
    public required string Detail { get; init; }
}
