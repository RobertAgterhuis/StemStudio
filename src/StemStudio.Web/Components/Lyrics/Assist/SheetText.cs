namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Positions in the editor text (anchors are 1-based lines with UTF-16 offsets within the line, backend-api 1.23.0 <c>Anchor</c>):
/// line of a cursor, offset of a line, the word at the cursor and the end of the block (lines up to the next blank line).
/// </summary>
public static class SheetText
{
    /// <summary>The lines of a text (<c>\r\n</c> and <c>\n</c>).</summary>
    /// <param name="text">The text.</param>
    /// <returns>The lines; an empty text has one empty line.</returns>
    public static string[] Lines(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');

    /// <summary>The 1-based line of a cursor offset.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The cursor offset (clamped to the text).</param>
    /// <returns>The line.</returns>
    public static int LineAt(string text, int offset)
    {
        var normalised = text.Replace("\r\n", "\n", StringComparison.Ordinal);
        var end = Math.Clamp(offset, 0, normalised.Length);
        return normalised.AsSpan(0, end).Count('\n') + 1;
    }

    /// <summary>The offset of the start of a 1-based line in the normalised text.</summary>
    /// <param name="text">The text.</param>
    /// <param name="line">The line (clamped to the text).</param>
    /// <returns>The offset.</returns>
    public static int OffsetOf(string text, int line)
    {
        var lines = Lines(text);
        var count = Math.Clamp(line, 1, lines.Length) - 1;
        return lines.Take(count).Sum(l => l.Length + 1);
    }

    /// <summary>The last line of the block that contains <paramref name="line"/> (the line before the next blank line).</summary>
    /// <param name="text">The text.</param>
    /// <param name="line">A 1-based line.</param>
    /// <returns>The block's last line.</returns>
    public static int BlockEnd(string text, int line)
    {
        var lines = Lines(text);
        var last = Math.Clamp(line, 1, lines.Length);
        while (last < lines.Length && lines[last].Trim().Length > 0)
        {
            last++;
        }

        return last;
    }

    /// <summary>The word around a cursor offset (letters, digits and apostrophes), or null.</summary>
    /// <param name="text">The text.</param>
    /// <param name="offset">The cursor offset.</param>
    /// <returns>The word.</returns>
    public static string? WordAt(string text, int offset)
    {
        var at = Math.Clamp(offset, 0, text.Length);
        var start = at;
        while (start > 0 && IsWordChar(text[start - 1]))
        {
            start--;
        }

        var end = at;
        while (end < text.Length && IsWordChar(text[end]))
        {
            end++;
        }

        var word = text[start..end].Trim('\'');
        return word.Length == 0 ? null : word;
    }

    /// <summary>True for a section marker line such as <c>[chorus]</c>.</summary>
    /// <param name="line">The line text.</param>
    /// <returns>Whether it is a marker.</returns>
    public static bool IsMarker(string line) => line.Trim() is { Length: > 1 } trimmed && trimmed[0] == '[' && trimmed[^1] == ']';

    private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c is '\'' or '’';
}
