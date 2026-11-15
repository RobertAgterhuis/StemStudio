using StemStudio.Api.Tracks;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Detail;

/// <summary>Maps <c>POST /api/tracks/{trackId}/detail</c> and <c>POST /api/tracks/{trackId}/detail/cancel</c> (backend 1.2.0).</summary>
public static class DetailEndpoints
{
    /// <summary>Maps the detail phase endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapDetailEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(TrackEndpoints.TrackRoute + "/detail", (Guid trackId, DetailService detail, CancellationToken ct) => detail.StartAsync(trackId, ct))
            .WithName("startDetail").WithTags("tracks").Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(TrackEndpoints.TrackRoute + "/detail/cancel", (Guid trackId, DetailService detail, CancellationToken ct) => detail.CancelAsync(trackId, ct))
            .WithName("cancelDetail").WithTags("tracks").Produces<TrackDto>()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
