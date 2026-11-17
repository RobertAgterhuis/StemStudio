using System.Text.RegularExpressions;

namespace StemStudio.Agent.Policy;

/// <summary>
/// Provenance checks (FR-013, FR-014, research R-05): a URL argument must be a literal substring of a user message
/// (whitespace normalised only); lyrics text must occur in a user message after the backend's lyrics normalisation (010
/// data-model 3.1: LRC timestamps and tag lines removed, line endings unified, lines trimmed, at most two blank lines).
/// </summary>
public static partial class ProvenanceChecker
{
    /// <summary>True when the URL was typed by the user.</summary>
    /// <param name="url">URL argument.</param>
    /// <param name="userMessages">User message texts of the conversation.</param>
    /// <returns>True when it occurs literally.</returns>
    public static bool UrlFromUser(string url, IEnumerable<string> userMessages)
    {
        var needle = NormalizeWhitespace(url);
        return needle.Length > 0 && userMessages.Any(message => NormalizeWhitespace(message).Contains(needle, StringComparison.Ordinal));
    }

    /// <summary>True when the lyrics were written by the user.</summary>
    /// <param name="lyrics">Lyrics argument.</param>
    /// <param name="userMessages">User message texts of the conversation.</param>
    /// <returns>True when they occur after normalisation.</returns>
    public static bool LyricsFromUser(string lyrics, IEnumerable<string> userMessages)
    {
        var needle = NormalizeLyrics(lyrics);
        return needle.Length > 0 && userMessages.Any(message => NormalizeLyrics(message).Contains(needle, StringComparison.Ordinal));
    }

    /// <summary>True when the word occurs as a whole word (case-insensitive) in a user message.</summary>
    /// <param name="word">The word argument.</param>
    /// <param name="userMessages">The user messages of the conversation.</param>
    /// <returns>True when the user named it.</returns>
    public static bool WordFromUser(string word, IEnumerable<string> userMessages)
    {
        var needle = word.Trim();
        return needle.Length > 0 && userMessages.Any(message =>
            WordPattern().Matches(message).Any(match => string.Equals(match.Value.Trim('\'', '’'), needle, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>Collapses every whitespace run to one space and trims.</summary>
    /// <param name="text">Text.</param>
    /// <returns>Normalised text.</returns>
    public static string NormalizeWhitespace(string text) => Whitespace().Replace(text, " ").Trim();

    /// <summary>The backend's lyrics normalisation (lines trimmed on both sides for the comparison).</summary>
    /// <param name="text">Lyrics.</param>
    /// <returns>Normalised lyrics (empty for null).</returns>
    public static string NormalizeLyrics(string? text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
            .Where(line => !TagLine().IsMatch(line.Trim()))
            .Select(line => Timestamps().Replace(line.Trim(), string.Empty).Trim());
        return string.Join('\n', CollapseBlankLines(lines)).Trim('\n');
    }

    private static List<string> CollapseBlankLines(IEnumerable<string> lines)
    {
        var result = new List<string>();
        var blanks = 0;
        foreach (var line in lines)
        {
            blanks = line.Length == 0 ? blanks + 1 : 0;
            if (blanks <= 2)
            {
                result.Add(line);
            }
        }

        return result;
    }

    [GeneratedRegex(@"[\p{L}\p{M}'’-]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"\A(\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\])+", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamps();

    [GeneratedRegex(@"\A\[(ar|ti|al|au|by|offset|length|re|ve|id|lr):[^\]]*\]\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TagLine();
}
