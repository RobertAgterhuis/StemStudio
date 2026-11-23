using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>Track.key</c>: the key next to the BPM (<c>TrackKeySummary</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record TrackKeySummaryDto
{
    /// <summary>Tonic pitch class.</summary>
    public required int Tonic { get; init; }

    /// <summary>Mode.</summary>
    public required KeyModeDto Mode { get; init; }

    /// <summary>Confidence 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Display text spelled by data-model 2.5, e.g. "Eb major" (at most 40 characters).</summary>
    public required string Display { get; init; }

    /// <summary>Number of key regions (&gt; 1 = the song changes key).</summary>
    public required int Regions { get; init; }
}
