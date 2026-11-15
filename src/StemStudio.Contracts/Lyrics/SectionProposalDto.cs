namespace StemStudio.Contracts.Lyrics;

/// <summary>Result of <c>POST /api/lyrics/sections</c> (1.9.0).</summary>
public sealed record SectionProposalDto
{
    /// <summary>The text with proposed markers.</summary>
    public required string Text { get; init; }

    /// <summary>The sections.</summary>
    public IReadOnlyList<LyricsSectionDto> Sections { get; init; } = [];

    /// <summary>Lyrics density for a 120 s song (warning above 4.0).</summary>
    public required double WordsPerSecondAt120s { get; init; }
}
