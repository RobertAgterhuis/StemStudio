using StemStudio.Contracts.Library;

namespace StemStudio.Api.Library;

/// <summary>
/// Matches search words against an item's instrument terms (feature 022, FR-023/FR-024, SC-006). A word matches a term
/// when it is part of the term's id words or label; a family alias ("bass guitar", "keyboard") matches only when its
/// words appear as consecutive search words (CTO decision 022: precise results, "guitar" never finds the bass family).
/// </summary>
public static class InstrumentMatcher
{
    /// <summary>The outcome for one item.</summary>
    /// <param name="Classes">Best match class per search word (-1 = none).</param>
    /// <param name="Matched">The matched terms, in term order.</param>
    public sealed record Result(IReadOnlyList<int> Classes, IReadOnlyList<InstrumentTerm> Matched)
    {
        /// <summary>Labels of the matched terms.</summary>
        public IEnumerable<string> Labels => Matched.Select(term => term.Label);

        /// <summary>Highlights of the words on the matched labels (computed for returned hits only).</summary>
        /// <param name="tokens">The folded search words.</param>
        /// <returns>The highlights.</returns>
        public IEnumerable<HighlightDto> Highlights(IReadOnlyList<string> tokens) => Matched.SelectMany(term =>
            tokens.Select(token => LibrarySearch.Highlight(SearchFieldDto.Instrument, term.FoldedLabel, token, term.Label)).OfType<HighlightDto>());

        /// <summary>The class of search word <paramref name="index"/>.</summary>
        /// <param name="index">Word index.</param>
        /// <returns>0..2 or -1.</returns>
        public int ClassOf(int index) => Classes[index];
    }

    /// <summary>Matches the words.</summary>
    /// <param name="terms">The item's terms.</param>
    /// <param name="tokens">The folded search words.</param>
    /// <returns>The outcome.</returns>
    public static Result Match(IReadOnlyList<InstrumentTerm> terms, IReadOnlyList<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(terms);
        ArgumentNullException.ThrowIfNull(tokens);
        var classes = Enumerable.Repeat(-1, tokens.Count).ToArray();
        var matched = terms.Where(term => MatchTerm(term, tokens, classes)).ToList();
        return new Result(classes, matched);
    }

    private static bool MatchTerm(InstrumentTerm term, IReadOnlyList<string> tokens, int[] classes)
    {
        var matched = false;
        for (var i = 0; i < tokens.Count; i++)
        {
            foreach (var word in term.Words)
            {
                matched |= Keep(classes, i, LibrarySearch.ClassOf(word, tokens[i]));
            }
        }

        foreach (var alias in term.Aliases)
        {
            matched |= MatchPhrase(alias, tokens, classes);
        }

        return matched;
    }

    private static bool MatchPhrase(string[] alias, IReadOnlyList<string> tokens, int[] classes)
    {
        var matched = false;
        for (var start = 0; start + alias.Length <= tokens.Count; start++)
        {
            if (Enumerable.Range(0, alias.Length).All(offset => tokens[start + offset] == alias[offset]))
            {
                Enumerable.Range(start, alias.Length).ToList().ForEach(i => classes[i] = 0);
                matched = true;
            }
        }

        return matched;
    }

    private static bool Keep(int[] classes, int index, int value)
    {
        if (value < 0)
        {
            return false;
        }

        classes[index] = classes[index] < 0 ? value : Math.Min(classes[index], value);
        return true;
    }
}
