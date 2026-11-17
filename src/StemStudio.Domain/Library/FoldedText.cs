namespace StemStudio.Domain.Library;

/// <summary>
/// A folded text (research R-02 steps 1-4) with the index map back to the original: <see cref="Map"/>[i] is the UTF-16
/// index in the original where folded character <c>i</c> starts, <see cref="Ends"/>[i] the exclusive original end
/// (marks removed after a letter belong to that letter). Used by the search to map highlights to the displayed text.
/// </summary>
public sealed class FoldedText
{
    internal FoldedText(string value, int[] map, int[] ends)
    {
        Value = value;
        Map = map;
        Ends = ends;
    }

    /// <summary>The folded text.</summary>
    public string Value { get; }

    /// <summary>Original start index per folded character.</summary>
    public IReadOnlyList<int> Map { get; }

    /// <summary>Original exclusive end index per folded character.</summary>
    public IReadOnlyList<int> Ends { get; }

    /// <summary>Maps a range of the folded text to the original text.</summary>
    /// <param name="start">Start in the folded text.</param>
    /// <param name="length">Length in the folded text (at least 1).</param>
    /// <returns>Start and length in the original (UTF-16).</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is outside the folded text.</exception>
    public (int Start, int Length) ToOriginal(int start, int length)
    {
        if (start < 0 || length < 1 || start + length > Value.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(start), "The range is outside the folded text.");
        }

        var originalStart = Map[start];
        return (originalStart, Ends[start + length - 1] - originalStart);
    }
}
