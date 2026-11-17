namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>Where a suggestion applies (data-model 4).</summary>
/// <param name="Line">1-based line in the normalised text; 0 for titles.</param>
/// <param name="Start">UTF-16 offset in the line.</param>
/// <param name="Length">Length of the original text; 0 for an insertion point.</param>
/// <param name="Original">The text at the anchor (at most 400 characters).</param>
public sealed record Anchor(int Line, int Start, int Length, string Original)
{
    /// <summary>Longest original text.</summary>
    public const int MaxOriginalLength = 400;

    /// <summary>True for an insertion point (continuations).</summary>
    public bool IsInsertionPoint => Length == 0;

    /// <summary>True for a title anchor.</summary>
    public bool IsTitle => Line == 0;

    /// <summary>End offset (exclusive) in the line.</summary>
    public int End => Start + Length;

    /// <summary>True when the original text is at this anchor in the line.</summary>
    /// <param name="lineText">The text of <see cref="Line"/>.</param>
    /// <returns>True when it matches.</returns>
    public bool MatchesIn(string lineText)
    {
        ArgumentNullException.ThrowIfNull(lineText);
        return IsInsertionPoint
            ? Start <= lineText.Length
            : Original.Length == Length && Start >= 0 && End <= lineText.Length && string.CompareOrdinal(lineText, Start, Original, 0, Length) == 0;
    }
}

/// <summary>Line helpers of normalised sheet text (LF line ends; 1-based lines).</summary>
public static class SheetText
{
    /// <summary>The lines of a text.</summary>
    /// <param name="text">Normalised text.</param>
    /// <returns>The lines (index 0 = line 1).</returns>
    public static string[] Lines(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Split('\n');
    }

    /// <summary>True for a section marker line (<c>[chorus]</c>).</summary>
    /// <param name="line">The line.</param>
    /// <returns>True for a marker.</returns>
    public static bool IsMarker(string line) => LyricsSections.Marker((line ?? string.Empty).Trim()) is not null;

    /// <summary>True for a line the assistant checks: not blank, not a marker.</summary>
    /// <param name="line">The line.</param>
    /// <returns>True for a lyric line.</returns>
    public static bool IsLyric(string line) => !string.IsNullOrWhiteSpace(line) && !IsMarker(line);

    /// <summary>Every start offset of <paramref name="value"/> in <paramref name="line"/> (ordinal, overlapping allowed).</summary>
    /// <param name="line">The line.</param>
    /// <param name="value">The text to find (not empty).</param>
    /// <returns>The offsets.</returns>
    public static IReadOnlyList<int> Occurrences(string line, string value)
    {
        ArgumentNullException.ThrowIfNull(line);
        var found = new List<int>();
        if (string.IsNullOrEmpty(value))
        {
            return found;
        }

        for (var index = line.IndexOf(value, StringComparison.Ordinal); index >= 0; index = line.IndexOf(value, index + 1, StringComparison.Ordinal))
        {
            found.Add(index);
        }

        return found;
    }
}
