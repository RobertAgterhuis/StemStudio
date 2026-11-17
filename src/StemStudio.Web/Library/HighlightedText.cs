using StemStudio.Contracts.Library;

namespace StemStudio.Web.Library;

/// <summary>A part of a displayed text: plain, or a matched part shown in a <c>&lt;mark&gt;</c> (search-mark).</summary>
/// <param name="Text">The characters.</param>
/// <param name="Marked">True for a matched part.</param>
public sealed record TextSegment(string Text, bool Marked);

/// <summary>
/// Splits a displayed text into plain and highlighted segments from the search highlights (feature 022, FR-025,
/// data-model 7). Ranges are UTF-16 offsets; ranges outside the text are clipped, overlapping or touching ranges are
/// merged and a range never splits a surrogate pair, so a bad range can never throw or garble the text.
/// </summary>
public static class HighlightedText
{
    /// <summary>The segments of one field.</summary>
    /// <param name="text">The displayed text of the field.</param>
    /// <param name="highlights">Every highlight of the hit.</param>
    /// <param name="field">The field whose highlights apply.</param>
    /// <param name="label">For <see cref="SearchFieldDto.Instrument"/>: the label the ranges belong to.</param>
    /// <returns>The segments in text order (one plain segment when nothing matched).</returns>
    public static IReadOnlyList<TextSegment> Split(string text, IEnumerable<HighlightDto> highlights, SearchFieldDto field, string? label = null)
    {
        var ranges = Ranges(text, highlights.Where(h => h.Field == field && (label is null || h.Label is null || h.Label == label)));
        var segments = new List<TextSegment>();
        var position = 0;
        foreach (var (start, end) in ranges)
        {
            AddPlain(segments, text, position, start);
            segments.Add(new TextSegment(text[start..end], true));
            position = end;
        }

        AddPlain(segments, text, position, text.Length);
        return segments;
    }

    private static void AddPlain(List<TextSegment> segments, string text, int from, int to)
    {
        if (to > from)
        {
            segments.Add(new TextSegment(text[from..to], false));
        }
    }

    private static List<(int Start, int End)> Ranges(string text, IEnumerable<HighlightDto> highlights)
    {
        var merged = new List<(int Start, int End)>();
        foreach (var (start, end) in highlights.Select(h => Clip(text, h)).Where(r => r.End > r.Start).OrderBy(r => r.Start))
        {
            if (merged.Count > 0 && start <= merged[^1].End)
            {
                merged[^1] = (merged[^1].Start, Math.Max(merged[^1].End, end));
            }
            else
            {
                merged.Add((start, end));
            }
        }

        return merged;
    }

    private static (int Start, int End) Clip(string text, HighlightDto highlight)
    {
        var start = Math.Clamp(highlight.Start, 0, text.Length);
        var end = Math.Clamp((long)highlight.Start + Math.Max(0, highlight.Length), start, text.Length);
        return (Widen(text, start, -1), Widen(text, (int)end, 1));
    }

    // Moves an offset that lands between the halves of a surrogate pair outward (start back, end forward).
    private static int Widen(string text, int offset, int direction) =>
        offset > 0 && offset < text.Length && char.IsLowSurrogate(text[offset]) ? offset + (direction < 0 ? -1 : 1) : offset;
}
