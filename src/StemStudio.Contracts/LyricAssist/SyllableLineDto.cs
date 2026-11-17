namespace StemStudio.Contracts.LyricAssist;

/// <summary>Syllables of one line (schema <c>SyllableLine</c>).</summary>
public sealed record SyllableLineDto
{
    /// <summary>1-based line.</summary>
    public required int Line { get; init; }

    /// <summary>Counted syllables (0 for markers and blank lines).</summary>
    public required int Syllables { get; init; }

    /// <summary>Section marker or blank line (not counted).</summary>
    public required bool IsMarker { get; init; }

    /// <summary>Section name, when known.</summary>
    public string? Section { get; init; }

    /// <summary>Median of the section's lines.</summary>
    public double? SectionMedian { get; init; }

    /// <summary>Target syllables from the rhythm target.</summary>
    public int? Target { get; init; }

    /// <summary>More than 2 from the target, or from the section median without a target.</summary>
    public required bool Deviates { get; init; }
}
