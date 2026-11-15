namespace StemStudio.Contracts.Generation;

/// <summary>The range a restyle was made from (1.8.0).</summary>
public sealed record SourceRangeDto
{
    /// <summary>Start in seconds.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>End in seconds.</summary>
    public required double EndSeconds { get; init; }

    /// <summary>Range or in-place.</summary>
    public required RestyleModeDto Mode { get; init; }
}
