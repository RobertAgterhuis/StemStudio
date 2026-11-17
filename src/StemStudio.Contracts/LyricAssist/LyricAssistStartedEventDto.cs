namespace StemStudio.Contracts.LyricAssist;

/// <summary>Data of the <c>assist.started</c> event.</summary>
public sealed record LyricAssistStartedEventDto
{
    /// <summary>The assist.</summary>
    public required Guid AssistId { get; init; }

    /// <summary>Layers that run.</summary>
    public required IReadOnlyList<LayerInfoDto> Layers { get; init; }

    /// <summary>Model backend (<c>ollama</c>, <c>claude</c>, <c>fake</c>) or null without a model layer.</summary>
    public string? Backend { get; init; }

    /// <summary>Model name.</summary>
    public string? Model { get; init; }

    /// <summary>True while the CPU fallback model is used.</summary>
    public required bool UsingFallback { get; init; }

    /// <summary>Model blocks to process.</summary>
    public required int BlocksTotal { get; init; }

    /// <summary>Estimated duration.</summary>
    public double? EstimatedSeconds { get; init; }
}
