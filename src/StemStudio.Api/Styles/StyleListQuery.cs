using StemStudio.Domain;
using StemStudio.Domain.Styles;

namespace StemStudio.Api.Styles;

/// <summary>Query values of <c>GET /api/styles</c>.</summary>
/// <param name="Kind"><c>preset</c> or <c>user</c>.</param>
/// <param name="GenreFamily">A genre family wire name.</param>
/// <param name="Mood">Mood tag (at most 30).</param>
/// <param name="Instrument">Instrument tag (at most 30).</param>
/// <param name="Q">Search text (at most 100).</param>
public sealed record StyleListQuery(string? Kind, string? GenreFamily, string? Mood, string? Instrument, string? Q)
{
    /// <summary>The domain filter; errors are added per field.</summary>
    /// <param name="errors">Collected errors.</param>
    /// <returns>The filter.</returns>
    public StyleFilter ToFilter(List<DomainError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        var kind = QueryParsing.Enum<StyleKind>(Kind, "kind", errors);
        var family = QueryParsing.Enum<Domain.Styles.GenreFamily>(GenreFamily, "genreFamily", errors);
        Limit(Mood, "mood", StyleRules.MaxTagLength, errors);
        Limit(Instrument, "instrument", StyleRules.MaxTagLength, errors);
        Limit(Q, "q", 100, errors);
        return new StyleFilter(kind, family, Mood, Instrument, Q);
    }

    private static void Limit(string? value, string field, int maxLength, List<DomainError> errors)
    {
        if (value?.Length > maxLength)
        {
            errors.Add(new DomainError(field, $"Must be at most {maxLength} characters."));
        }
    }
}
