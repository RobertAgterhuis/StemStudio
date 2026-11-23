namespace StemStudio.Contracts.InstrumentViews;

/// <summary>The capo that keeps the original shapes after a transpose (backend-api 1.15.0, feature 016, FR-077).</summary>
public sealed record CapoShortcutDto
{
    /// <summary>Capo fret 1..7.</summary>
    public required int Capo { get; init; }

    /// <summary>Always <c>original-shapes</c>.</summary>
    public string Reason { get; init; } = OriginalShapes;

    /// <summary>The only reason value.</summary>
    public const string OriginalShapes = "original-shapes";
}
