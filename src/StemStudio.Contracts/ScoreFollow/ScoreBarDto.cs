namespace StemStudio.Contracts.ScoreFollow;

/// <summary>One bar of a score view: box in page fractions, times in track seconds (<c>ScoreBar</c>) (backend-api 1.13.0, feature 014).</summary>
public sealed record ScoreBarDto
{
    /// <summary>0-based bar index in document order.</summary>
    public required int Index { get; init; }

    /// <summary>Printed bar number (0 for a pickup).</summary>
    public required int Number { get; init; }

    /// <summary>Page of the bar.</summary>
    public required int Page { get; init; }

    /// <summary>Left edge, 0..1 of the page width.</summary>
    public required double X { get; init; }

    /// <summary>Top edge, 0..1 of the page height.</summary>
    public required double Y { get; init; }

    /// <summary>Width, (0..1].</summary>
    public required double Width { get; init; }

    /// <summary>Height, (0..1].</summary>
    public required double Height { get; init; }

    /// <summary>Start in track seconds.</summary>
    public required double StartSeconds { get; init; }

    /// <summary>End in track seconds (the next bar's start).</summary>
    public required double EndSeconds { get; init; }
}
