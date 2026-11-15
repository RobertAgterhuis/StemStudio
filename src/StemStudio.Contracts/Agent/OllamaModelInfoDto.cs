namespace StemStudio.Contracts.Agent;

/// <summary>A model installed in the host's Ollama.</summary>
public sealed record OllamaModelInfoDto
{
    /// <summary>Ollama model name, e.g. <c>qwen3.5:9b</c>.</summary>
    public required string Name { get; init; }

    /// <summary>Size on disk in bytes.</summary>
    public required long SizeBytes { get; init; }

    /// <summary>True when the model reports the <c>tools</c> capability (only these are selectable).</summary>
    public required bool Tools { get; init; }
}
