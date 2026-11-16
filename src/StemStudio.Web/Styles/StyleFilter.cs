using StemStudio.Contracts.Styles;

namespace StemStudio.Web.Styles;

/// <summary>
/// Filters and search of the style library (009 FR-004, US2-2): genre family, mood, instrument, kind and a text that
/// matches name, sub-genre, description and tags (case-insensitive). The library order (presets first, then by name)
/// is kept.
/// </summary>
public sealed class StyleFilter
{
    /// <summary>Only this genre family.</summary>
    public GenreFamilyDto? Genre { get; set; }

    /// <summary>Only styles with this mood tag.</summary>
    public string? Mood { get; set; }

    /// <summary>Only styles with this instrument tag.</summary>
    public string? Instrument { get; set; }

    /// <summary>Only presets or only own styles.</summary>
    public StyleKindDto? Kind { get; set; }

    /// <summary>Search text.</summary>
    public string Search { get; set; } = string.Empty;

    /// <summary>True when any filter or search is set.</summary>
    public bool IsActive => new object?[] { Genre, Mood, Instrument, Kind }.Any(value => value is not null) || Search.Trim().Length > 0;

    /// <summary>The distinct mood tags of the library, sorted.</summary>
    /// <param name="styles">The library.</param>
    /// <returns>The moods.</returns>
    public static IReadOnlyList<string> Moods(IEnumerable<StyleDto> styles) => Tags(styles.SelectMany(s => s.Moods));

    /// <summary>The distinct instrument tags of the library, sorted.</summary>
    /// <param name="styles">The library.</param>
    /// <returns>The instruments.</returns>
    public static IReadOnlyList<string> Instruments(IEnumerable<StyleDto> styles) => Tags(styles.SelectMany(s => s.Instrumentation));

    /// <summary>The styles that pass every filter, in the given order.</summary>
    /// <param name="styles">The library.</param>
    /// <returns>The matching styles.</returns>
    public IReadOnlyList<StyleDto> Apply(IEnumerable<StyleDto> styles) => [.. styles.Where(Matches)];

    private bool Matches(StyleDto style) =>
        MatchesKind(style) && HasTag(style.Moods, Mood) && HasTag(style.Instrumentation, Instrument) && MatchesSearch(style);

    private bool MatchesKind(StyleDto style) => (Genre is null || style.GenreFamily == Genre) && (Kind is null || style.Kind == Kind);

    private bool MatchesSearch(StyleDto style)
    {
        var text = Search.Trim();
        return text.Length == 0
            || new[] { style.Name, style.SubGenre, style.Description }.Concat(style.Moods).Concat(style.Instrumentation)
                .Any(field => field?.Contains(text, StringComparison.OrdinalIgnoreCase) == true);
    }

    private static bool HasTag(IReadOnlyList<string> tags, string? wanted) =>
        wanted is null || tags.Contains(wanted, StringComparer.OrdinalIgnoreCase);

    private static string[] Tags(IEnumerable<string> tags) =>
        [.. tags.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase)];
}
