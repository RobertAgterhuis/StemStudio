namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One drum hit (backend-api 1.15.0, feature 016, data-model 5.1).</summary>
public sealed record DrumHitDto
{
    /// <summary>Time in ms.</summary>
    public required int TimeMs { get; init; }

    /// <summary>Piece.</summary>
    public required DrumPieceDto Piece { get; init; }

    /// <summary>Strength 0..1.</summary>
    public required double Strength { get; init; }
}
