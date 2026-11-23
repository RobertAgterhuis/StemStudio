namespace StemStudio.Contracts.InstrumentViews;

/// <summary>One capo suggestion (backend-api 1.15.0, feature 016, research R-14).</summary>
public sealed record CapoSuggestionDto
{
    /// <summary>Capo fret 0..7.</summary>
    public required int Capo { get; init; }

    /// <summary>Key of the shapes, e.g. "C major".</summary>
    public required string ShapeKey { get; init; }

    /// <summary>Share 0..1 of the song's time in open shapes.</summary>
    public required double OpenShare { get; init; }

    /// <summary>True when the best suggestion still has few open shapes.</summary>
    public required bool FewOpenShapes { get; init; }
}
