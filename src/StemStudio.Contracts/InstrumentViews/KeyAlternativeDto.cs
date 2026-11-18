using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The relative key that fits almost as well (backend-api 1.15.0, feature 016, research R-13).</summary>
public sealed record KeyAlternativeDto
{
    /// <summary>Tonic pitch class (C = 0).</summary>
    public required int Tonic { get; init; }

    /// <summary>Mode.</summary>
    public required KeyModeDto Mode { get; init; }
}
