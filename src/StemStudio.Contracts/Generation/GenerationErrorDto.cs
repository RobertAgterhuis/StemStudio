namespace StemStudio.Contracts.Generation;

/// <summary>Why a generation failed (1.8.0).</summary>
public sealed record GenerationErrorDto
{
    /// <summary>Error code.</summary>
    public required GenerationErrorCodeDto Code { get; init; }

    /// <summary>Readable message.</summary>
    public required string Message { get; init; }
}
