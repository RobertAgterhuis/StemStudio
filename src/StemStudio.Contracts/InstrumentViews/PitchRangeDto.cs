namespace StemStudio.Contracts.InstrumentViews;

/// <summary>5th/95th percentile of the voiced reference frames (backend-api 1.15.0, feature 016).</summary>
public sealed record PitchRangeDto
{
    /// <summary>Low value in centi-semitones.</summary>
    public required int LowCents { get; init; }

    /// <summary>High value in centi-semitones.</summary>
    public required int HighCents { get; init; }
}
