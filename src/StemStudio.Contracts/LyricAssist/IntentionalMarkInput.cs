namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST /api/lyrics/{lyricsId}/intentional-marks</c> (schema <c>IntentionalMarkInput</c>).</summary>
public sealed record IntentionalMarkInput
{
    /// <summary>Scope.</summary>
    public required IntentionalScopeDto Scope { get; init; }

    /// <summary>Word 1-40 without spaces, phrase 1-200, line 1-400; must occur in the current text.</summary>
    public required string Text { get; init; }

    /// <summary>Optional note (at most 120 characters).</summary>
    public string? Note { get; init; }
}
