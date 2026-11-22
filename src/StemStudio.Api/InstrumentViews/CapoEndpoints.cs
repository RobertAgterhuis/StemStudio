using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Scores;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Maps <c>getCapoSuggestions</c> (backend 1.15.0, scope addition S12, research R-14; FR-075..FR-077).</summary>
public static class CapoEndpoints
{
    /// <summary>The route of the capo suggestions.</summary>
    public const string Route = "/api/tracks/{trackId:guid}/chords/capo-suggestions";

    /// <summary>Maps the capo suggestions.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapCapoEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, (Guid trackId, int? transpose, ChordService chords, CancellationToken ct) => GetAsync(chords, trackId, transpose ?? 0, ct))
            .WithName("getCapoSuggestions").WithTags(AnalysisEndpoints.Tag).Produces<CapoSuggestionsDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    /// <summary>Up to three capo positions with the most open shapes, best first, and the shortcut for the transpose.</summary>
    /// <param name="chords">The chord service.</param>
    /// <param name="trackId">Track id.</param>
    /// <param name="transpose">The 012 transpose.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404, 409 <c>chords-not-ready</c> or 422 (transpose out of range).</returns>
    public static async Task<IResult> GetAsync(ChordService chords, Guid trackId, int transpose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chords);
        if (InstrumentViewProblems.DisplayOutOfRange(transpose, null) is { } invalid)
        {
            return invalid;
        }

        var (ready, problem) = await chords.FindReadyAsync(trackId, cancellationToken);
        return problem ?? TypedResults.Ok(ToDto(CapoAdvisor.Advise(Timed(ready!.Chart), KeyOf(ready.Chart), transpose), transpose));
    }

    /// <summary>The chords with a root and quality, with their durations ("N" segments are left out).</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The timed chords.</returns>
    public static IReadOnlyList<TimedChord> Timed(ChordChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        return chart.Segments
            .Where(segment => segment is { Root: not null, Quality: not null })
            .Select(segment => new TimedChord(segment.Root!.Value, segment.Quality!.Value, segment.EndMs - segment.StartMs))
            .ToList();
    }

    /// <summary>The key the shapes are named in: the chart's key, else C major.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The key.</returns>
    public static MusicalKey KeyOf(ChordChart chart)
    {
        ArgumentNullException.ThrowIfNull(chart);
        return chart.Key?.Key ?? new MusicalKey(0, KeyMode.Major);
    }

    private static CapoSuggestionsDto ToDto(CapoAdvice advice, int transpose) => new()
    {
        Transpose = transpose,
        Shortcut = advice.Shortcut is { } capo ? new CapoShortcutDto { Capo = capo } : null,
        Suggestions = advice.Suggestions
            .Select(suggestion => new CapoSuggestionDto { Capo = suggestion.Capo, ShapeKey = suggestion.ShapeKey, OpenShare = suggestion.OpenShare, FewOpenShapes = suggestion.FewOpenShapes })
            .ToList(),
    };
}
