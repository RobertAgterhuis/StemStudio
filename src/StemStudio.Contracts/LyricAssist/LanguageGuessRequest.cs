namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST /api/lyric-assist/language-guess</c>.</summary>
public sealed record LanguageGuessRequest
{
    /// <summary>The text (1-65,536 characters).</summary>
    public required string Text { get; init; }
}
