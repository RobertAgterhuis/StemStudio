namespace StemStudio.Contracts.LyricAssist;

/// <summary>One rhyme (schema <c>Rhyme</c>).</summary>
public sealed record RhymeDto
{
    /// <summary>The word (at most 40 characters).</summary>
    public required string Word { get; init; }

    /// <summary>Quality.</summary>
    public required RhymeKindDto Kind { get; init; }

    /// <summary>Syllables of the word.</summary>
    public required int Syllables { get; init; }
}
