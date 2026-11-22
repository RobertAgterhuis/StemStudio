using StemStudio.Api.Scores;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Sections;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Maps <c>getKey</c> (backend 1.15.0, scope addition S11; FR-070..FR-074).</summary>
public static class KeyEndpoints
{
    /// <summary>The route of the key view.</summary>
    public const string Route = "/api/tracks/{trackId:guid}/key";

    /// <summary>Largest transpose either way (012).</summary>
    public const int MaxTranspose = 12;

    /// <summary>Maps the key view.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapKeyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, (Guid trackId, int? transpose, string? instrument, TrackAnalysisService service, CancellationToken ct) =>
                GetAsync(service, trackId, new KeyQuery(transpose ?? 0, instrument), ct))
            .WithName("getKey").WithTags(AnalysisEndpoints.Tag).Produces<KeyViewDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    /// <summary>The key view of a track.</summary>
    /// <param name="service">The analysis service.</param>
    /// <param name="trackId">Track id.</param>
    /// <param name="query">Transpose and written instrument as sent.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 404 or 422 (transpose or instrument outside the catalogue).</returns>
    public static async Task<IResult> GetAsync(TrackAnalysisService service, Guid trackId, KeyQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(query);
        var instrument = ScoreNotationWire.FromWire<WrittenInstrumentDto>(query.Instrument ?? "concert");
        if (QueryErrors(query, instrument) is { } problem)
        {
            return problem;
        }

        return await service.GetAsync(trackId, AnalysisKind.Chords, cancellationToken) is { } view
            ? TypedResults.Ok(KeyViewMapper.ToDto(view, query.Transpose, (WrittenInstrument)(int)instrument!.Value))
            : TrackService.NotFound(trackId);
    }

    private static IResult? QueryErrors(KeyQuery query, WrittenInstrumentDto? instrument)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (Math.Abs(query.Transpose) > MaxTranspose)
        {
            errors["transpose"] = [$"Must be between -{MaxTranspose} and {MaxTranspose}."];
        }

        if (instrument is null)
        {
            errors["instrument"] = ["Must be a written instrument of the catalogue, e.g. concert or alto-sax-eb."];
        }

        return errors.Count == 0 ? null : ApiProblems.Unprocessable(errors);
    }
}

/// <summary>The query of <c>getKey</c> as sent.</summary>
/// <param name="Transpose">The 012 transpose.</param>
/// <param name="Instrument">The written instrument id, or null for <c>concert</c>.</param>
public sealed record KeyQuery(int Transpose, string? Instrument);

/// <summary>Builds the <c>KeyView</c> of a chords analysis (data-model 2.8): the key and regions exist while the chart is ready or stale.</summary>
public static class KeyViewMapper
{
    /// <summary>The key view.</summary>
    /// <param name="view">The chords analysis view.</param>
    /// <param name="transpose">The transpose.</param>
    /// <param name="instrument">The written instrument.</param>
    /// <returns>The DTO.</returns>
    public static KeyViewDto ToDto(AnalysisView view, int transpose, WrittenInstrument instrument)
    {
        ArgumentNullException.ThrowIfNull(view);
        var empty = new KeyViewDto { Status = InstrumentViewMapper.ToDto(view.Status), Transpose = transpose, Key = null, Display = null, WrittenKey = null, Regions = [] };
        return ShownChart(view) is { Key: { } key } chart
            ? empty with
            {
                Key = InstrumentViewMapper.ToDto(key),
                Display = KeyFormatter.Display(key.Key, transpose, key.Confidence),
                WrittenKey = KeyFormatter.WrittenKey(Transposed(key.Key, transpose), instrument),
                Regions = chart.KeyRegions.Select(region => Region(region, transpose, view.Track.BeatGrid)).ToList(),
            }
            : empty;
    }

    private static ChordChart? ShownChart(AnalysisView view) => view.Status is AnalysisStatus.Ready or AnalysisStatus.Stale ? view.Stored?.Document as ChordChart : null;

    private static MusicalKey Transposed(MusicalKey key, int transpose) => key with { Tonic = PitchClass.Of(key.Tonic + transpose) };

    private static KeyViewRegionDto Region(KeyRegion region, int transpose, BeatGrid? grid) => new()
    {
        StartMs = region.StartMs,
        EndMs = region.EndMs,
        StartBar = grid is { Reliable: true } ? SectionSnapper.BarAt(region.StartMs / 1000.0, grid) : null,
        Tonic = region.Tonic,
        Mode = InstrumentViewMapper.ToDto(region.Mode),
        Confidence = region.Confidence,
        Display = KeyFormatter.Name(Transposed(new MusicalKey(region.Tonic, region.Mode), transpose)),
    };
}
