namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The stored reference melody (backend-api 1.15.0, feature 016, data-model 3.1).</summary>
public sealed record VocalPitchDocumentDto
{
    /// <summary>Frame hop in ms (always 10).</summary>
    public int HopMs { get; init; } = 10;

    /// <summary>Centi-semitones per frame (MIDI x 100), 0 = unvoiced (at most 90,000).</summary>
    public required IReadOnlyList<int> Frames { get; init; }

    /// <summary>Source stem.</summary>
    public required VocalSourceDto Source { get; init; }

    /// <summary>Share of voiced frames 0..1.</summary>
    public required double VoicedRatio { get; init; }

    /// <summary>Range of the voiced frames, or null.</summary>
    public required PitchRangeDto? Range { get; init; }

    /// <summary>Warnings.</summary>
    public required IReadOnlyList<AnalysisWarningDto> Warnings { get; init; }
}
