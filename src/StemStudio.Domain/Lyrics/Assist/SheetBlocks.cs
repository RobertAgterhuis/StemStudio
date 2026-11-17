namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>One block of lines reviewed by one model request.</summary>
/// <param name="Index">0-based index.</param>
/// <param name="FirstLine">First line (1-based, inclusive; a leading section marker belongs to the block).</param>
/// <param name="LastLine">Last lyric line (1-based, inclusive).</param>
public sealed record SheetBlock(int Index, int FirstLine, int LastLine);

/// <summary>
/// Splits a sheet into model blocks (research R-05): sections and blank lines end a block, a block holds at most
/// <c>maxLines</c> lyric lines (markers and blank lines do not count), blocks without lyric lines are dropped.
/// </summary>
public static class SheetBlocks
{
    /// <summary>Default lyric lines per block.</summary>
    public const int DefaultMaxLines = 8;

    /// <summary>The blocks of a text, optionally limited to a line range.</summary>
    /// <param name="text">Normalised text.</param>
    /// <param name="maxLines">Lyric lines per block (at least 1).</param>
    /// <param name="lineFrom">First line of the range, or null.</param>
    /// <param name="lineTo">Last line of the range, or null.</param>
    /// <returns>The blocks in order.</returns>
    public static IReadOnlyList<SheetBlock> Split(string text, int maxLines = DefaultMaxLines, int? lineFrom = null, int? lineTo = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLines, 1);
        var lines = SheetText.Lines(text);
        var ranges = Ranges(lines, maxLines);
        var from = lineFrom ?? 1;
        var to = lineTo ?? lines.Length;
        return ranges
            .Select(range => (First: Math.Max(range.First, from), Last: Math.Min(range.Last, to)))
            .Where(range => HasLyric(lines, range.First, range.Last))
            .Select((range, index) => new SheetBlock(index, range.First, range.Last))
            .ToList();
    }

    private static List<(int First, int Last)> Ranges(string[] lines, int maxLines)
    {
        var builder = new BlockBuilder(maxLines);
        for (var line = 1; line <= lines.Length; line++)
        {
            builder.Add(line, lines[line - 1]);
        }

        builder.Close();
        return builder.Ranges;
    }

    private static bool HasLyric(string[] lines, int first, int last) =>
        first <= last && Enumerable.Range(first, last - first + 1).Any(line => SheetText.IsLyric(lines[line - 1]));

    private sealed class BlockBuilder(int maxLines)
    {
        private int? _start;
        private int _last;
        private int _lyrics;

        public List<(int First, int Last)> Ranges { get; } = [];

        public void Add(int line, string text)
        {
            if (string.IsNullOrWhiteSpace(text) || SheetText.IsMarker(text))
            {
                Close();
                _start = string.IsNullOrWhiteSpace(text) ? null : line;
                return;
            }

            if (_lyrics == maxLines)
            {
                Close();
            }

            _start ??= line;
            (_last, _lyrics) = (line, _lyrics + 1);
        }

        public void Close()
        {
            if (_lyrics > 0)
            {
                Ranges.Add((_start!.Value, _last));
            }

            (_start, _lyrics) = (null, 0);
        }
    }
}
