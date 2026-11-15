namespace StemStudio.Contracts.Agent;

/// <summary>Ollama probe result (GET /api/tags and /api/show, 2 s timeout each).</summary>
public sealed record OllamaStatusDto
{
    /// <summary>True when Ollama answered.</summary>
    public required bool Reachable { get; init; }

    /// <summary>Ollama version, when reachable.</summary>
    public string? Version { get; init; }

    /// <summary>Installed models (empty when unreachable).</summary>
    public IReadOnlyList<OllamaModelInfoDto> Models { get; init; } = [];

    /// <summary>True when the configured default model is installed with tool support.</summary>
    public required bool DefaultPresent { get; init; }

    /// <summary>True when the configured CPU fallback model is installed with tool support.</summary>
    public required bool FallbackPresent { get; init; }
}
