using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Sort orders of the track library (<c>tracks-sort</c> option values).</summary>
public enum TrackSort
{
    /// <summary>Newest import first (default).</summary>
    Newest,

    /// <summary>Oldest import first.</summary>
    Oldest,

    /// <summary>Title A-Z.</summary>
    Title,

    /// <summary>Artist A-Z, tracks without artist last.</summary>
    Artist,
}

/// <summary>Client-side search and sort of the track library (design-spec.md 4.3, Advanced+).</summary>
public static class TrackListQuery
{
    /// <summary>Parses a <c>tracks-sort</c> value; unknown values sort by newest.</summary>
    /// <param name="value">The option value, e.g. <c>title</c>.</param>
    /// <returns>The sort.</returns>
    public static TrackSort ParseSort(string? value) =>
        Enum.TryParse<TrackSort>(value, ignoreCase: true, out var sort) && Enum.IsDefined(sort) ? sort : TrackSort.Newest;

    /// <summary>Filters by title or artist (case-insensitive) and sorts.</summary>
    /// <param name="tracks">All tracks.</param>
    /// <param name="search">Search text; blank keeps everything.</param>
    /// <param name="sort">The order.</param>
    /// <returns>The visible tracks.</returns>
    public static IReadOnlyList<TrackDto> Apply(IEnumerable<TrackDto> tracks, string? search, TrackSort sort)
    {
        var term = search?.Trim() ?? string.Empty;
        var matching = tracks.Where(t => term.Length == 0 || Contains(t.Title, term) || Contains(t.Artist, term));
        return Order(matching, sort).ToList();
    }

    private static bool Contains(string? text, string term) => text?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private static IEnumerable<TrackDto> Order(IEnumerable<TrackDto> tracks, TrackSort sort) => sort switch
    {
        TrackSort.Oldest => tracks.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id),
        TrackSort.Title => tracks.OrderBy(t => t.Title, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id),
        TrackSort.Artist => tracks.OrderBy(t => t.Artist is null).ThenBy(t => t.Artist, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id),
        _ => tracks.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id),
    };
}
