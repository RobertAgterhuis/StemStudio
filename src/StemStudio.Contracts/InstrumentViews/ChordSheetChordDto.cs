namespace StemStudio.Contracts.InstrumentViews;

/// <summary>A chord anchored in a chord sheet row (backend-api 1.15.0, feature 016, data-model 2.6).</summary>
public sealed record ChordSheetChordDto
{
    /// <summary>Character index in the row text.</summary>
    public required int CharIndex { get; init; }

    /// <summary>Display symbol (at most 24 characters).</summary>
    public required string Symbol { get; init; }

    /// <summary>The chord starts before the first word of the line.</summary>
    public required bool Lead { get; init; }
}
