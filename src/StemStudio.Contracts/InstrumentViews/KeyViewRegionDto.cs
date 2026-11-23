using StemStudio.Contracts.Scores;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One region of <c>KeyView.regions</c> (backend-api 1.15.0, feature 016).</summary>
public sealed record KeyViewRegionDto
{
    /// <summary>Start in ms.</summary>
    public required int StartMs { get; init; }

    /// <summary>End in ms.</summary>
    public required int EndMs { get; init; }

    /// <summary>1-based bar of the beat grid; null without a reliable grid.</summary>
    public required int? StartBar { get; init; }

    /// <summary>Tonic pitch class.</summary>
    public required int Tonic { get; init; }

    /// <summary>Mode.</summary>
    public required KeyModeDto Mode { get; init; }

    /// <summary>Confidence 0..1.</summary>
    public required double Confidence { get; init; }

    /// <summary>Display spelled at the requested transpose.</summary>
    public required string Display { get; init; }
}
