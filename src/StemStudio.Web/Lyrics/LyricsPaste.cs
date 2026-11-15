using StemStudio.Contracts;

namespace StemStudio.Web.Lyrics;

/// <summary>Client-side check of lyrics entered by the user (backend 1.6.0 <c>SetLyricsRequest</c>); the backend checks the rest.</summary>
public static class LyricsPaste
{
    /// <summary>Message for empty text.</summary>
    public const string EmptyMessage = "Enter the lyrics, one line per sung line.";

    /// <summary>Message for text over the limit.</summary>
    public static readonly string TooLongMessage = $"Lyrics can have at most {ContractLimits.MaxLyricsChars} characters.";

    /// <summary>Message for control characters.</summary>
    public const string ControlMessage = "The text contains control characters; paste plain text only.";

    /// <summary>Checks pasted text.</summary>
    /// <param name="text">The text as typed.</param>
    /// <returns>A readable problem, or null.</returns>
    public static string? Validate(string text) =>
        text.Trim().Length == 0 ? EmptyMessage
        : text.Length > ContractLimits.MaxLyricsChars ? TooLongMessage
        : text.Any(IsForbidden) ? ControlMessage
        : null;

    private static bool IsForbidden(char c) => char.IsControl(c) && c is not ('\n' or '\r' or '\t');
}
