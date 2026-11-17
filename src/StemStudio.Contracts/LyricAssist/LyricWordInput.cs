namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST /api/lyric-assist/words</c> (schema <c>LyricWordInput</c>).</summary>
public sealed record LyricWordInput
{
    /// <summary>Language.</summary>
    public required AssistLanguageDto Language { get; init; }

    /// <summary>The word, 1-40 characters without whitespace.</summary>
    public required string Word { get; init; }
}
