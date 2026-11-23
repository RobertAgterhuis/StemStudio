using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The global song key (<c>SongKey</c>) (backend-api 1.15.0, feature 016, S11, data-model 2.8).</summary>
public sealed record SongKeyDto
{
    /// <summary>Tonic pitch class (C = 0).</summary>
    public required int Tonic { get; init; }

    /// <summary>Mode.</summary>
    public required KeyModeDto Mode { get; init; }

    /// <summary>Confidence 0..1 (&lt; 0.5 shows "(uncertain)").</summary>
    public required double Confidence { get; init; }

    /// <summary>The relative alternative, or null.</summary>
    public required KeyAlternativeDto? Alternative { get; init; }
}
