using System.Text.RegularExpressions;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Line and word helpers over the normalised sheet text (LF line ends, 1-based lines as in the anchors).</summary>
public sealed partial class LyricText
{
    private readonly string[] _lines;

    /// <summary>Splits the text into lines.</summary>
    /// <param name="text">Normalised sheet text.</param>
    public LyricText(string text) => _lines = (text ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    /// <summary>Number of lines.</summary>
    public int Count => _lines.Length;

    /// <summary>The text of a 1-based line, or null when the line does not exist.</summary>
    /// <param name="line">1-based line.</param>
    /// <returns>The line text.</returns>
    public string? Line(int line) => line >= 1 && line <= _lines.Length ? _lines[line - 1] : null;

    /// <summary>True for a line that may carry a suggestion: it exists, is not blank and is not a section marker.</summary>
    /// <param name="line">1-based line.</param>
    /// <returns>True when reviewable.</returns>
    public bool IsReviewable(int line) => Line(line) is { } text && !string.IsNullOrWhiteSpace(text) && !IsMarker(text);

    /// <summary>True for a section marker line such as <c>[chorus]</c> or <c>[verse 2]</c> (never checked or changed).</summary>
    /// <param name="text">A line.</param>
    /// <returns>True for a marker.</returns>
    public static bool IsMarker(string text) => Marker().IsMatch(text.Trim());

    /// <summary>The words of a text (letters, marks, apostrophes and inner hyphens).</summary>
    /// <param name="text">Any text.</param>
    /// <returns>The words in order.</returns>
    public static IReadOnlyList<string> Words(string? text) =>
        [.. Word().Matches(text ?? string.Empty).Select(match => match.Value.Trim('\'', '’', '-')).Where(word => word.Length > 0)];

    /// <summary>The last word of a text, or null.</summary>
    /// <param name="text">Any text.</param>
    /// <returns>The last word.</returns>
    public static string? LastWord(string? text) => Words(text) is { Count: > 0 } words ? words[^1] : null;

    [GeneratedRegex(@"\A\[[^\[\]]{1,40}\]\z", RegexOptions.CultureInvariant)]
    private static partial Regex Marker();

    [GeneratedRegex(@"[\p{L}\p{M}'’-]+", RegexOptions.CultureInvariant)]
    private static partial Regex Word();
}
