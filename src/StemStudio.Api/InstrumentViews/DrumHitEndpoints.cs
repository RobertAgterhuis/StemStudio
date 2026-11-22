using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;

namespace StemStudio.Api.InstrumentViews;

/// <summary>The operations of an analysis without request options: one route with GET, POST (start or re-run) and DELETE.</summary>
/// <typeparam name="TDto">The DTO type.</typeparam>
/// <param name="Route">The route template.</param>
/// <param name="Projection">Kind and DTO mapping.</param>
/// <param name="Names">Operation ids of the GET, the POST and the DELETE.</param>
/// <param name="KindText">The kind in problem texts, e.g. "drum hits".</param>
public sealed record PlainAnalysisRoute<TDto>(string Route, AnalysisProjection<TDto> Projection, (string Get, string Start, string Delete) Names, string KindText);

/// <summary>Maps the analyses that take no request body (drum hits, the reference melody).</summary>
public static class PlainAnalysisEndpoints
{
    /// <summary>Maps the GET, POST and DELETE of an analysis.</summary>
    /// <typeparam name="TDto">The DTO type.</typeparam>
    /// <param name="app">The route builder.</param>
    /// <param name="route">The route and its names.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapPlainAnalysis<TDto>(this IEndpointRouteBuilder app, PlainAnalysisRoute<TDto> route)
    {
        ArgumentNullException.ThrowIfNull(route);
        var kind = route.Projection.Kind;
        app.MapGet(route.Route, (Guid trackId, TrackAnalysisService service, CancellationToken ct) => AnalysisEndpoints.GetAsync(service, trackId, route.Projection, ct))
            .WithName(route.Names.Get).WithTags(AnalysisEndpoints.Tag).Produces<TDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(route.Route, (Guid trackId, TrackAnalysisService service, CancellationToken ct) =>
                AnalysisEndpoints.StartAsync(service, new AnalysisStart(trackId, kind, TrackAnalysisService.DefaultOptions(kind), false, route.KindText), route.Projection.Map, ct))
            .WithName(route.Names.Start).RequireGateOpen().WithTags(AnalysisEndpoints.Tag).Produces<TDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapDelete(route.Route, (Guid trackId, TrackAnalysisService service, CancellationToken ct) => AnalysisEndpoints.DeleteAsync(service, trackId, kind, ct))
            .WithName(route.Names.Delete).RequireGateOpen().WithTags(AnalysisEndpoints.Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}

/// <summary>Maps <c>getDrumHits</c>, <c>detectDrumHits</c> and <c>deleteDrumHits</c> (backend 1.15.0; FR-030).</summary>
public static class DrumHitEndpoints
{
    /// <summary>The route of the drum hits.</summary>
    public const string Route = "/api/tracks/{trackId:guid}/drum-hits";

    /// <summary>Maps the drum hits operations.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapDrumHitEndpoints(this IEndpointRouteBuilder app) => app.MapPlainAnalysis(new PlainAnalysisRoute<TrackAnalysisDto<DrumHitsDocumentDto>>(
        Route,
        new(AnalysisKind.DrumHits, InstrumentViewMapper.ToDrumHitsDto),
        ("getDrumHits", "detectDrumHits", "deleteDrumHits"),
        "drum hits"));
}
