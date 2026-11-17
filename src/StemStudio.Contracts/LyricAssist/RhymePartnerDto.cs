namespace StemStudio.Contracts.LyricAssist;

/// <summary>The verified rhyme partner of a device suggestion (<c>LyricSuggestion.rhymeWith</c>).</summary>
public sealed record RhymePartnerDto
{
    /// <summary>1-based line of the partner word.</summary>
    public required int Line { get; init; }

    /// <summary>The partner word (at most 40 characters).</summary>
    public required string Word { get; init; }
}
