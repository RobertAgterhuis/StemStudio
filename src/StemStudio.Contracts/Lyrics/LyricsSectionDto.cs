namespace StemStudio.Contracts.Lyrics;

/// <summary>A section found in lyrics (1.9.0).</summary>
public sealed record LyricsSectionDto
{
    /// <summary>Marker.</summary>
    public required SectionMarkerDto Marker { get; init; }

    /// <summary>Number of a numbered marker (<c>[verse 2]</c>).</summary>
    public int? Number { get; init; }

    /// <summary>Lines in the section.</summary>
    public required int LineCount { get; init; }
}
