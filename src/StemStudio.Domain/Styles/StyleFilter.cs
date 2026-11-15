namespace StemStudio.Domain.Styles;

/// <summary>Library filters; they combine with AND, blank texts are ignored (backend <c>listStyles</c>).</summary>
/// <param name="Kind">Preset or user.</param>
/// <param name="Family">Genre family.</param>
/// <param name="Mood">A mood tag (case-insensitive equality).</param>
/// <param name="Instrument">An instrument tag (case-insensitive equality).</param>
/// <param name="Query">Case-insensitive substring of the name, description or a tag.</param>
public sealed record StyleFilter(StyleKind? Kind, GenreFamily? Family, string? Mood, string? Instrument, string? Query)
{
    /// <summary>Whether a style passes every filter.</summary>
    /// <param name="style">The style.</param>
    /// <returns>True when it matches.</returns>
    public bool Matches(Style style)
    {
        ArgumentNullException.ThrowIfNull(style);
        return MatchesKinds(style) && HasTag(style.Moods, Mood) && HasTag(style.Instrumentation, Instrument) && MatchesQuery(style);
    }

    private bool MatchesKinds(Style style) => (Kind is null || style.Kind == Kind) && (Family is null || style.GenreFamily == Family);

    /// <summary>Presets first, then by name (case-insensitive).</summary>
    /// <param name="styles">The styles.</param>
    /// <returns>The ordered list.</returns>
    public static IReadOnlyList<Style> Order(IEnumerable<Style> styles) =>
        styles.OrderBy(style => style.IsPreset ? 0 : 1).ThenBy(style => style.NormalizedName, StringComparer.Ordinal).ToList();

    private static bool HasTag(IReadOnlyList<string> tags, string? wanted) =>
        string.IsNullOrWhiteSpace(wanted) || tags.Contains(wanted.Trim(), StringComparer.OrdinalIgnoreCase);

    private bool MatchesQuery(Style style)
    {
        if (string.IsNullOrWhiteSpace(Query))
        {
            return true;
        }

        var query = Query.Trim();
        return new[] { style.Name, style.Description ?? string.Empty }.Concat(style.Instrumentation).Concat(style.Moods)
            .Any(text => text.Contains(query, StringComparison.OrdinalIgnoreCase));
    }
}
