namespace StemStudio.Contracts.Generation;

/// <summary>The generation a library item came from (origin generation, 1.8.0).</summary>
public sealed record GenerationProvenanceDto
{
    /// <summary>The generation.</summary>
    public required Guid GenerationId { get; init; }

    /// <summary>The take.</summary>
    public required int TakeIndex { get; init; }

    /// <summary>Kind of the generation.</summary>
    public required GenerationKindDto Kind { get; init; }
}
