namespace StemStudio.Domain.Lyrics;

/// <summary>The credit shown with the lyrics (data-model 3.3): the source, plus how the timing was made.</summary>
public static class LyricsCredit
{
    /// <summary>The credit of a document.</summary>
    /// <param name="document">The document, or null.</param>
    /// <returns>E.g. "Lyrics: LRCLIB; word timing estimated", or null without a document.</returns>
    public static string? For(LyricsDocument? document) => document is null ? null : SourceText(document.Source) + OriginText(document.Origin);

    private static string SourceText(LyricsSource source) => source == LyricsSource.User ? "Lyrics: entered by you" : "Lyrics: LRCLIB";

    private static string OriginText(LyricsOrigin origin) => origin switch
    {
        LyricsOrigin.Interpolated => "; word timing estimated",
        LyricsOrigin.Aligned => "; timing: automatic alignment",
        _ => string.Empty,
    };
}
