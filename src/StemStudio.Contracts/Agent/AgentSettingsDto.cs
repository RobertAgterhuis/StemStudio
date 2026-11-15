namespace StemStudio.Contracts.Agent;

/// <summary>Agent settings (one row; data-model 2). Contains nothing secret.</summary>
public sealed record AgentSettingsDto
{
    /// <summary>Selected backend (<c>fake</c> only through the environment).</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Default Ollama model (GPU), default <c>qwen3.5:9b</c>.</summary>
    public required string OllamaModel { get; init; }

    /// <summary>CPU fallback model while a GPU job runs, default <c>qwen3.5:4b</c>.</summary>
    public required string OllamaFallbackModel { get; init; }

    /// <summary>Ollama thinking on or off (default off).</summary>
    public required bool OllamaThink { get; init; }

    /// <summary>Claude model (default claude-opus-5-5).</summary>
    public required ClaudeModelDto ClaudeModel { get; init; }

    /// <summary>Claude effort (default low).</summary>
    public required ClaudeEffortDto ClaudeEffort { get; init; }

    /// <summary>True when the paid-service notice was acknowledged.</summary>
    public required bool ClaudeNoticeAcknowledged { get; init; }

    /// <summary>Per-run limits.</summary>
    public required AgentLimitsDto Limits { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
