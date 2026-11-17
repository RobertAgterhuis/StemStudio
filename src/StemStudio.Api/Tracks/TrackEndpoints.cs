using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tracks;

/// <summary>
/// Maps <c>GET /api/tracks</c>, <c>GET /api/tracks/{trackId}</c>, <c>DELETE /api/tracks/{trackId}</c> and
/// <c>POST /api/tracks/{trackId}/retry</c>.
/// </summary>
public static class TrackEndpoints
{
    /// <summary>Route of a single track with a GUID constraint.</summary>
    public const string TrackRoute = ApiRoutes.Tracks + "/{trackId:guid}";

    /// <summary>Maps the track endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapTrackEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Tracks, (TrackService tracks, CancellationToken ct) => tracks.ListAsync(ct))
            .WithName("listTracks").WithTags("tracks").Produces<List<TrackDto>>();
        app.MapGet(TrackRoute, (Guid trackId, TrackService tracks, CancellationToken ct) => tracks.GetAsync(trackId, ct))
            .WithName("getTrack").WithTags("tracks").Produces<TrackDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapDelete(TrackRoute, (Guid trackId, TrackDeletionService deletion, CancellationToken ct) => deletion.DeleteAsync(trackId, ct))
            .WithName("deleteTrack").RequireGateOpen().WithTags("tracks").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(TrackRoute + "/retry", (Guid trackId, TrackService tracks, CancellationToken ct) => tracks.RetryAsync(trackId, ct))
            .WithName("retryTrack").RequireGateOpen().WithTags("tracks").Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
