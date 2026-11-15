using System.Text.RegularExpressions;

namespace StemStudio.Domain.Lyrics;

/// <summary>
/// Normalises own lyrics (010 data-model 3.1): LRC timestamps and LRC tag lines removed, CR/CRLF to LF, trailing
/// spaces trimmed, at most two consecutive blank lines, no blank lines at the start or end.
/// </summary>
public static partial class LyricsNormaliser
{
    /// <summary>The normalised text.</summary>
    /// <param name="text">The text as typed or uploaded.</param>
    /// <returns>The normalised text (empty for null).</returns>
    public static string Normalize(string? text)
    {
        var lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n')
            .Where(line => !TagLine().IsMatch(line.Trim()))
            .Select(line => Timestamps().Replace(line, string.Empty).TrimEnd());
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

    [GeneratedRegex(@"\A(\[\d{1,3}:\d{2}(?:[.:]\d{1,3})?\])+", RegexOptions.CultureInvariant)]
    private static partial Regex Timestamps();

    [GeneratedRegex(@"\A\[(ar|ti|al|au|by|offset|length|re|ve|id|lr):[^\]]*\]\z", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex TagLine();
}
