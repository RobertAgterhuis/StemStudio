using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Input of one model call.</summary>
public sealed record LyricModelRequest
{
    /// <summary>The assist kind (selects the prompt family and the allowed types).</summary>
    public required AssistKindDto Kind { get; init; }

    /// <summary>0-based block index (the scripted fakes answer by index).</summary>
    public required int BlockIndex { get; init; }

    /// <summary>System prompt (licence rules, schema, explanation language).</summary>
    public required string SystemPrompt { get; init; }

    /// <summary>User prompt: the sheet as delimited data with the block marked.</summary>
    public required string UserPrompt { get; init; }

    /// <summary>Sampling temperature (0.2 for checks, 0.8 for writing help).</summary>
    public required double Temperature { get; init; }

    /// <summary>The backend and model fixed for the assist; a call on another backend is refused (no switch mid-assist).</summary>
    public LyricModelInfo? Model { get; init; }

    /// <summary>Output token cap of this call (Claude: what is left of the per-assist budget); null = the default.</summary>
    public int? MaxOutputTokens { get; init; }
}
