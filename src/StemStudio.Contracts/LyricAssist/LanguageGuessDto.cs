namespace StemStudio.Contracts.LyricAssist;

/// <summary>Language guess for an <c>unknown</c> sheet (schema <c>LanguageGuess</c>).</summary>
public sealed record LanguageGuessDto
{
    /// <summary>Guessed language; null when neither reaches <see cref="LyricAssistLimits.LanguageGuessMinHitRate"/> dictionary hits.</summary>
    public AssistLanguageDto? Language { get; init; }

    /// <summary>Share of dictionary hits, 0-1.</summary>
    public required double Confidence { get; init; }
}
