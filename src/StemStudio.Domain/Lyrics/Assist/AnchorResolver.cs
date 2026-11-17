namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>
/// Re-anchors open suggestions after a sheet save (FR-008, data-model 4): keep the anchor when the original is still at
/// <c>(Line, Start)</c> of the line it moved to (line LCS diff between revisions), else the unique occurrence on that line,
/// else the unique occurrence within +-2 lines; otherwise the suggestion is stale.
/// </summary>
public sealed class AnchorResolver
{
    /// <summary>How far an anchor may move to another line.</summary>
    public const int LineWindow = 2;

    private readonly string[] _newLines;
    private readonly int?[] _map;

    /// <summary>Creates a resolver from the old and the new text.</summary>
    /// <param name="oldText">Normalised text the anchors refer to.</param>
    /// <param name="newText">Normalised new text.</param>
    public AnchorResolver(string oldText, string newText)
    {
        var oldLines = SheetText.Lines(oldText);
        _newLines = SheetText.Lines(newText);
        _map = LineDiff.Map(oldLines, _newLines);
    }

    /// <summary>The anchor in the new text.</summary>
    /// <param name="anchor">Anchor in the old text.</param>
    /// <returns>The new anchor, or null when the suggestion is stale.</returns>
    public Anchor? Resolve(Anchor anchor)
    {
        ArgumentNullException.ThrowIfNull(anchor);
        if (anchor.IsTitle)
        {
            return anchor;
        }

        var oldIndex = anchor.Line - 1;
        if (oldIndex < 0 || oldIndex >= _map.Length)
        {
            return null;
        }

        return anchor.IsInsertionPoint ? ResolveInsertion(anchor, _map[oldIndex]) : ResolveText(anchor, Target(oldIndex));
    }

    private Anchor? ResolveInsertion(Anchor anchor, int? mapped) =>
        mapped is { } line ? anchor with { Line = line + 1, Start = _newLines[line].Length } : null;

    private Anchor? ResolveText(Anchor anchor, int target)
    {
        if (target >= 0 && target < _newLines.Length && anchor.MatchesIn(_newLines[target]))
        {
            return anchor with { Line = target + 1 };
        }

        return Unique(anchor, target, target) ?? Unique(anchor, target - LineWindow, target + LineWindow);
    }

    private Anchor? Unique(Anchor anchor, int first, int last)
    {
        var (low, high) = (Math.Max(first, 0), Math.Min(last, _newLines.Length - 1));
        var hits = Enumerable.Range(low, Math.Max(high - low + 1, 0))
            .SelectMany(line => SheetText.Occurrences(_newLines[line], anchor.Original).Select(start => (Line: line, Start: start)))
            .Take(2)
            .ToList();
        return hits.Count == 1 ? anchor with { Line = hits[0].Line + 1, Start = hits[0].Start } : null;
    }

    private int Target(int oldIndex)
    {
        if (_map[oldIndex] is { } mapped)
        {
            return mapped;
        }

        for (var previous = oldIndex - 1; previous >= 0; previous--)
        {
            if (_map[previous] is { } anchorLine)
            {
                return anchorLine + (oldIndex - previous);
            }
        }

        return oldIndex;
    }
}

/// <summary>Longest common subsequence of lines: which old line is which new line.</summary>
public static class LineDiff
{
    /// <summary>For every old line the index of the equal new line in the LCS, or null when it changed or was deleted.</summary>
    /// <param name="oldLines">Old lines.</param>
    /// <param name="newLines">New lines.</param>
    /// <returns>The mapping (index = old line index).</returns>
    public static int?[] Map(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        ArgumentNullException.ThrowIfNull(oldLines);
        ArgumentNullException.ThrowIfNull(newLines);
        var lengths = Lengths(oldLines, newLines);
        var map = new int?[oldLines.Count];
        for (int i = 0, j = 0; i < oldLines.Count && j < newLines.Count;)
        {
            if (string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal))
            {
                map[i++] = j++;
            }
            else if (lengths[i + 1, j] >= lengths[i, j + 1])
            {
                i++;
            }
            else
            {
                j++;
            }
        }

        return map;
    }

    /// <summary>Number of lines of <paramref name="newLines"/> that are not in the LCS with <paramref name="oldLines"/>.</summary>
    /// <param name="oldLines">Old lines.</param>
    /// <param name="newLines">New lines.</param>
    /// <returns>Changed or added lines.</returns>
    public static int ChangedLines(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var common = Map(oldLines, newLines).Count(index => index is not null);
        return Math.Max(oldLines.Count, newLines.Count) - common;
    }

    private static int[,] Lengths(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines)
    {
        var lengths = new int[oldLines.Count + 1, newLines.Count + 1];
        for (var i = oldLines.Count - 1; i >= 0; i--)
        {
            for (var j = newLines.Count - 1; j >= 0; j--)
            {
                lengths[i, j] = string.Equals(oldLines[i], newLines[j], StringComparison.Ordinal)
                    ? lengths[i + 1, j + 1] + 1
                    : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        return lengths;
    }
}
