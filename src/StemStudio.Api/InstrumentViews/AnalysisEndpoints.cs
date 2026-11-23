using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>Maps the instrument-view operations of backend 1.15.0 (tag <c>instrument-views</c>).</summary>
public static class AnalysisEndpoints
{
    /// <summary>The OpenAPI tag.</summary>
    public const string Tag = "instrument-views";

    private const string Track = "/api/tracks/{trackId:guid}";
    private const string Chords = Track + "/chords";

    /// <summary>Maps the chords operations (<c>getChords</c>, <c>detectChords</c>, <c>deleteChords</c>, <c>editChordSegment</c>, <c>getChordSheet</c>, <c>downloadChordPro</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapChordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Chords, (Guid trackId, TrackAnalysisService service, CancellationToken ct) => GetAsync(service, trackId, new AnalysisProjection<TrackAnalysisDto<ChordChartDocumentDto>>(AnalysisKind.Chords, InstrumentViewMapper.ToChordsDto), ct))
            .WithName("getChords").WithTags(Tag).Produces<TrackAnalysisDto<ChordChartDocumentDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapChordRunEndpoints().MapChordViewEndpoints();
    }

    /// <summary>Maps <c>detectChords</c> and <c>deleteChords</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    private static IEndpointRouteBuilder MapChordRunEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Chords, (Guid trackId, DetectChordsRequest? request, TrackAnalysisService service, CancellationToken ct) =>
                DetectChordsAsync(service, trackId, request ?? new DetectChordsRequest(), ct))
            .WithName("detectChords").RequireGateOpen().WithTags(Tag).Produces<TrackAnalysisDto<ChordChartDocumentDto>>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapDelete(Chords, (Guid trackId, TrackAnalysisService service, CancellationToken ct) => DeleteAsync(service, trackId, AnalysisKind.Chords, ct))
            .WithName("deleteChords").RequireGateOpen().WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    /// <summary>Maps <c>editChordSegment</c>, <c>getChordSheet</c> and <c>downloadChordPro</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    private static IEndpointRouteBuilder MapChordViewEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut(Chords + "/segments/{segmentIndex:int}", (Guid trackId, int segmentIndex, EditChordRequest request, ChordService chords, CancellationToken ct) =>
                chords.EditAsync(trackId, segmentIndex, request, ct))
            .WithName("editChordSegment").RequireGateOpen().WithTags(Tag).Produces<TrackAnalysisDto<ChordChartDocumentDto>>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(Chords + "/sheet", (Guid trackId, int? transpose, int? capo, ChordService chords, CancellationToken ct) =>
                chords.SheetAsync(trackId, Display(transpose, capo), ct))
            .WithName("getChordSheet").WithTags(Tag).Produces<ChordSheetDto>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapGet(Chords + "/chordpro", (Guid trackId, int? transpose, int? capo, ChordService chords, CancellationToken ct) =>
                chords.ChordProAsync(trackId, Display(transpose, capo), ct))
            .WithName("downloadChordPro").WithTags(Tag).Produces<string>(StatusCodes.Status200OK, "text/plain").ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    /// <summary>Maps <c>getInstrumentCatalogue</c> (static, <c>Cache-Control: max-age=3600</c>).</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapCatalogueEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(Contracts.ApiRoutes.InstrumentCatalogue, () => new HeaderResult(TypedResults.Ok(CatalogueMapper.Catalogue), "Cache-Control", "max-age=3600"))
            .WithName("getInstrumentCatalogue").WithTags(Tag).Produces<InstrumentCatalogueDto>();
        return app;
    }

    /// <summary>The GET of an analysis.</summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="service">The service.</param>
    /// <param name="trackId">Track id.</param>
    /// <param name="projection">Kind and DTO mapping.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public static async Task<IResult> GetAsync<TDto>(TrackAnalysisService service, Guid trackId, AnalysisProjection<TDto> projection, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(projection);
        return await service.GetAsync(trackId, projection.Kind, cancellationToken) is { } view ? TypedResults.Ok(projection.Map(view)) : TrackService.NotFound(trackId);
    }

    /// <summary>The POST of an analysis.</summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="service">The service.</param>
    /// <param name="request">The start request parts.</param>
    /// <param name="map">The DTO mapping.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404, 409 or 503.</returns>
    public static async Task<IResult> StartAsync<TDto>(TrackAnalysisService service, AnalysisStart request, Func<AnalysisView, TDto> map, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(map);
        var result = await service.StartAsync(request, cancellationToken);
        return result.Outcome == StartOutcome.TrackNotFound
            ? TrackService.NotFound(request.TrackId)
            : InstrumentViewProblems.For(result.Outcome, request.KindText) ?? TypedResults.Accepted((string?)null, map(result.View!));
    }

    /// <summary>The DELETE of an analysis.</summary>
    /// <param name="service">The service.</param>
    /// <param name="trackId">Track id.</param>
    /// <param name="kind">Kind.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>204 or 404.</returns>
    public static async Task<IResult> DeleteAsync(TrackAnalysisService service, Guid trackId, AnalysisKind kind, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(service);
        return await service.DeleteAsync(trackId, kind, cancellationToken) ? TypedResults.NoContent() : TrackService.NotFound(trackId);
    }

    private static ChordDisplay Display(int? transpose, int? capo) => new(transpose ?? 0, capo ?? 0);

    private static Task<IResult> DetectChordsAsync(TrackAnalysisService service, Guid trackId, DetectChordsRequest request, CancellationToken cancellationToken)
    {
        if (request.Engine == ChordEngineDto.Fake)
        {
            return Task.FromResult(ApiProblems.Unprocessable(new Dictionary<string, string[]>(StringComparer.Ordinal) { ["engine"] = ["Must be btc, chroma-hmm or null."] }));
        }

        var engine = request.Engine switch
        {
            ChordEngineDto.Btc => "btc",
            ChordEngineDto.ChromaHmm => "chroma-hmm",
            _ => null,
        };
        var options = new AnalysisOptions(request.Vocabulary == ChordVocabularyDto.Full ? ChordVocabulary.Full : ChordVocabulary.Simple, engine);
        return StartAsync(service, new AnalysisStart(trackId, AnalysisKind.Chords, options, request.OverwriteEdits, "chords"), InstrumentViewMapper.ToChordsDto, cancellationToken);
    }
}

/// <summary>The kind of an analysis and how its view maps to the wire DTO.</summary>
/// <typeparam name="TDto">The DTO type.</typeparam>
/// <param name="Kind">The kind.</param>
/// <param name="Map">The DTO mapping.</param>
public sealed record AnalysisProjection<TDto>(AnalysisKind Kind, Func<AnalysisView, TDto> Map);
