namespace StemStudio.Contracts.Generation;

/// <summary>Model that produced a take (1.8.0).</summary>
public sealed record GenerationTakeModelDto
{
    /// <summary>Model id.</summary>
    public required string Id { get; init; }

    /// <summary>Pinned revision.</summary>
    public required string Revision { get; init; }
}
