namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One row of a chord sheet (backend-api 1.15.0, feature 016).</summary>
public sealed record ChordSheetRowDto
{
    /// <summary>Lyric line, or empty for instrumental rows.</summary>
    public required string Text { get; init; }

    /// <summary>Chords of the row (at most 40).</summary>
    public required IReadOnlyList<ChordSheetChordDto> Chords { get; init; }
}
