using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>A key region of at least 8 bars (<c>KeyRegion</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record KeyRegionDto
{
    /// <summary>Start in ms.</summary>
    public required int StartMs { get; init; }

    /// <summary>End in ms.</summary>
    public required int EndMs { get; init; }

    /// <summary>Tonic pitch class.</summary>
    public required int Tonic { get; init; }

    /// <summary>Mode.</summary>
    public required KeyModeDto Mode { get; init; }

    /// <summary>Confidence 0..1.</summary>
    public required double Confidence { get; init; }
}
