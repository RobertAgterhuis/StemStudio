using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>Persisted agent settings (data-model 2); the API key is never part of it.</summary>
public sealed record AgentSettings
{
    /// <summary>Selected backend.</summary>
    public required AgentBackendDto Backend { get; init; }

    /// <summary>Default Ollama model (GPU).</summary>
    public required string OllamaModel { get; init; }

    /// <summary>CPU fallback model while a heavy GPU job runs.</summary>
    public required string OllamaFallbackModel { get; init; }

    /// <summary>Ollama thinking on or off.</summary>
    public required bool OllamaThink { get; init; }

    /// <summary>Claude model.</summary>
    public required ClaudeModelDto ClaudeModel { get; init; }

    /// <summary>Claude effort.</summary>
    public required ClaudeEffortDto ClaudeEffort { get; init; }

    /// <summary>When the paid-service notice was acknowledged; null = never.</summary>
    public DateTimeOffset? ClaudeNoticeAcknowledgedAt { get; init; }

    /// <summary>Per-run limits.</summary>
    public required AgentLimitsDto Limits { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Default Ollama model (research F-02).</summary>
    public const string DefaultOllamaModel = "qwen3.5:9b";

    /// <summary>Default CPU fallback model (research F-05).</summary>
    public const string DefaultOllamaFallbackModel = "qwen3.5:4b";

    /// <summary>True when Claude may be selected: key present and notice acknowledged (constitution 1.3.0).</summary>
    /// <param name="keyConfigured">True when ANTHROPIC_API_KEY is set.</param>
    /// <returns>True when the Claude backend is allowed.</returns>
    public bool AllowsClaude(bool keyConfigured) => keyConfigured && ClaudeNoticeAcknowledgedAt is not null;

    /// <summary>The settings used when no row exists yet (data-model 2).</summary>
    /// <param name="backend">Initial backend from <c>StemStudio__Agent__Backend</c> (default ollama).</param>
    /// <param name="now">Current time.</param>
    /// <returns>Default settings.</returns>
    public static AgentSettings Defaults(AgentBackendDto backend, DateTimeOffset now) => new()
    {
        Backend = backend,
        OllamaModel = DefaultOllamaModel,
        OllamaFallbackModel = DefaultOllamaFallbackModel,
        OllamaThink = false,
        ClaudeModel = ClaudeModelDto.ClaudeOpus55,
        ClaudeEffort = ClaudeEffortDto.Low,
        Limits = AgentLimitsDto.Default,
        UpdatedAt = now,
    };
}
