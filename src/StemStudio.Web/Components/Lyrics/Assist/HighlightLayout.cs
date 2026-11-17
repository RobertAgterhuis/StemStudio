using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>One piece of a highlighted line: plain text, or the span of an open suggestion.</summary>
/// <param name="Text">The text of the piece.</param>
/// <param name="Suggestion">The suggestion it highlights, or null for plain text.</param>
public sealed record HighlightSegment(string Text, LyricSuggestionDto? Suggestion);

/// <summary>
/// Splits a line of the editor text into plain and highlighted pieces for the overlay (FR-028: the category is shown by the
/// underline style, not by colour alone). Spans that overlap an earlier one or fall outside the line are left out; a
/// whole-line insertion or title suggestion has no span.
/// </summary>
public static class HighlightLayout
{
    /// <summary>The pieces of one line.</summary>
    /// <param name="line">The line text.</param>
    /// <param name="suggestions">Open suggestions anchored on this line.</param>
    /// <returns>The pieces in text order; together they spell the line.</returns>
    public static IReadOnlyList<HighlightSegment> Segments(string line, IEnumerable<LyricSuggestionDto> suggestions)
    {
        var pieces = new List<HighlightSegment>();
        var at = 0;
        foreach (var s in suggestions.Where(s => Fits(line, s)).OrderBy(s => s.Anchor.Start))
        {
            if (s.Anchor.Start >= at)
            {
                Add(pieces, line[at..s.Anchor.Start], null);
                Add(pieces, line.Substring(s.Anchor.Start, s.Anchor.Length), s);
                at = s.Anchor.Start + s.Anchor.Length;
            }
        }

        Add(pieces, line[at..], null);
        return pieces;
    }

    /// <summary>The CSS class of a category's underline style.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The class name.</returns>
    public static string StyleClass(SuggestionCategoryDto category) => "ss-hl-" + AssistText.Wire(category);

    private static bool Fits(string line, LyricSuggestionDto s) =>
        s.Operation == SuggestionOperationDto.Replace && s.Anchor.Length > 0 && s.Anchor.Start >= 0 && s.Anchor.Start + s.Anchor.Length <= line.Length;

    private static void Add(List<HighlightSegment> pieces, string text, LyricSuggestionDto? suggestion)
    {
        if (text.Length > 0)
        {
            pieces.Add(new HighlightSegment(text, suggestion));
        }
    }
}
