using System.Globalization;
using System.Text;

namespace StemStudio.Web.Help;

/// <summary>
/// In-memory help search (research.md R-06): lower-cased, diacritics folded, hyphenated terms kept; per query
/// term 5 x title + 3 x keyword + 2 x summary + 1 x body, a prefix match counts half; ties by title.
/// </summary>
public sealed class HelpSearch : IHelpSearch
{
    private const double Title = 5;
    private const double Keyword = 3;
    private const double Summary = 2;
    private const double Body = 1;
    private const double PrefixFactor = 0.5;

    private readonly List<Entry> _entries;

    /// <summary>Indexes every topic of the catalogue once.</summary>
    /// <param name="catalog">The help catalogue.</param>
    public HelpSearch(IHelpCatalog catalog) =>
        _entries = catalog.Topics.Select(t => new Entry(
            t.Id,
            t.Title,
            [
                (Title, Tokens(t.Title)),
                (Keyword, t.Keywords.SelectMany(Tokens).ToHashSet(StringComparer.Ordinal)),
                (Summary, Tokens(t.Summary)),
                (Body, Tokens(t.Markdown)),
            ])).ToList();

    /// <inheritdoc />
    public IReadOnlyList<HelpHit> Search(string query, int max = 10)
    {
        var terms = Tokens(query);
        if (terms.Count == 0)
        {
            return [];
        }

        return _entries
            .Select(e => (Entry: e, Score: e.Score(terms)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Entry.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Entry.Id, StringComparer.Ordinal)
            .Take(max)
            .Select(r => new HelpHit(r.Entry.Id, r.Score))
            .ToList();
    }

    /// <summary>Splits text into lower-case, diacritic-free terms (letters, digits and inner hyphens).</summary>
    /// <param name="text">Any text.</param>
    /// <returns>The distinct terms.</returns>
    internal static HashSet<string> Tokens(string text)
    {
        var folded = Fold(text.ToLowerInvariant());
        var words = new StringBuilder(folded.Length);
        foreach (var c in folded)
        {
            words.Append(char.IsLetterOrDigit(c) || c == '-' ? c : ' ');
        }

        return words.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => w.Trim('-'))
            .Where(w => w.Length > 0)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Fold(string text) =>
        string.Concat(text.Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark));

    private sealed record Entry(string Id, string Title, (double Weight, HashSet<string> Terms)[] Fields)
    {
        public double Score(IEnumerable<string> terms) =>
            terms.Sum(term => Fields.Sum(field => field.Weight * Match(term, field.Terms)));

        private static double Match(string term, HashSet<string> terms) =>
            terms.Contains(term) ? 1 : terms.Any(t => t.StartsWith(term, StringComparison.Ordinal)) ? PrefixFactor : 0;
    }
}
