namespace StemStudio.Contracts.Agent;

/// <summary>Readiness of the selected backend (GET /api/agent/status, always 200).</summary>
public sealed record AgentStatusDto
{
    /// <summary>Selected backend.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>True when the selected backend can run a command now.</summary>
    public required bool Ready { get; init; }

    /// <summary>Model the next turn would use; null when not ready or the backend is none.</summary>
    public string? ActiveModel { get; init; }

    /// <summary>True while the Ollama CPU fallback model is used because a GPU job runs.</summary>
    public required bool UsingFallback { get; init; }

    /// <summary>Readable reason when not ready.</summary>
    public string? Reason { get; init; }

    /// <summary>Readable fix when not ready, e.g. <c>ollama pull qwen3.5:9b (6.6 GB)</c>.</summary>
    public string? Fix { get; init; }

    /// <summary>Ollama probe result.</summary>
    public required OllamaStatusDto Ollama { get; init; }

    /// <summary>Claude state (never the key).</summary>
    public required ClaudeStatusDto Claude { get; init; }
}
