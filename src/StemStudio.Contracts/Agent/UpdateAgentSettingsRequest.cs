namespace StemStudio.Contracts.Agent;

/// <summary>Body of PUT /api/agent/settings; omitted fields keep their value.</summary>
public sealed record UpdateAgentSettingsRequest
{
    /// <summary>New backend (ollama, claude or none); claude needs the key (409) and the notice (422).</summary>
    public AgentBackendDto? Backend { get; init; }

    /// <summary>New default Ollama model; must be installed with tool support (422).</summary>
    public string? OllamaModel { get; init; }

    /// <summary>New fallback model; same validation.</summary>
    public string? OllamaFallbackModel { get; init; }

    /// <summary>Ollama thinking.</summary>
    public bool? OllamaThink { get; init; }

    /// <summary>Claude model.</summary>
    public ClaudeModelDto? ClaudeModel { get; init; }

    /// <summary>Claude effort.</summary>
    public ClaudeEffortDto? ClaudeEffort { get; init; }

    /// <summary>Replaces all limits at once.</summary>
    public AgentLimitsDto? Limits { get; init; }

    /// <summary>True to record that the user read the paid-service notice.</summary>
    public bool AcknowledgeClaudeNotice { get; init; }
}
