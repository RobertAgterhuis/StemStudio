namespace StemStudio.Contracts.Generation;

/// <summary>A model file set the generator needs (1.8.0).</summary>
public sealed record GenerationModelDto
{
    /// <summary>Model id, e.g. <c>acestep-v15-turbo</c>.</summary>
    public required string Id { get; init; }

    /// <summary>True when the files are in the model cache.</summary>
    public required bool Present { get; init; }

    /// <summary>Download size in bytes.</summary>
    public required long Bytes { get; init; }
}
