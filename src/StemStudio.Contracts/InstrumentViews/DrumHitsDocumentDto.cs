namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The stored drum hit map (backend-api 1.15.0, feature 016).</summary>
public sealed record DrumHitsDocumentDto
{
    /// <summary>Pieces that had an input.</summary>
    public required IReadOnlyList<DrumPieceDto> Pieces { get; init; }

    /// <summary>Hits in time order (at most 40,000).</summary>
    public required IReadOnlyList<DrumHitDto> Hits { get; init; }
}
