namespace StemStudio.Contracts.LyricAssist;

/// <summary>One assistant layer and whether it is usable (schema <c>LayerInfo</c>).</summary>
public sealed record LayerInfoDto
{
    /// <summary>The layer.</summary>
    public required SuggestionSourceDto Layer { get; init; }

    /// <summary>Display name, e.g. <c>OpenTaal (nl)</c> or <c>qwen3.5:9b (local)</c>; at most 100 characters.</summary>
    public required string Name { get; init; }

    /// <summary>True when the layer can run now.</summary>
    public required bool Available { get; init; }

    /// <summary>Readable reason when not available.</summary>
    public string? Reason { get; init; }

    /// <summary>Readable fix when not available.</summary>
    public string? Fix { get; init; }
}
