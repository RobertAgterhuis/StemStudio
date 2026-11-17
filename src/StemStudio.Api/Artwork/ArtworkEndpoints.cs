using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tracks;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Artwork;

/// <summary>Maps the artwork routes of backend 1.4.0 (feature 005).</summary>
public static class ArtworkEndpoints
{
    /// <summary>Route of the picture resource of a track.</summary>
    public const string ArtworkRoute = TrackEndpoints.TrackRoute + "/artwork";

    /// <summary>Maps <c>GET /api/tracks/{trackId}/artwork/{variant}</c>, <c>PUT</c> and <c>DELETE /api/tracks/{trackId}/artwork</c>.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapArtworkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ArtworkRoute + "/{variant}", (Guid trackId, string variant, string? v, ArtworkService artwork, CancellationToken ct) =>
                artwork.GetAsync(trackId, variant, v, ct))
            .WithName("getTrackArtwork").WithTags("artwork")
            .Produces(StatusCodes.Status200OK, contentType: ArtworkService.ContentType)
            .Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapOwnArtworkEndpoints();
    }

    private static IEndpointRouteBuilder MapOwnArtworkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut(ArtworkRoute, (Guid trackId, HttpRequest request, UserArtworkService artwork, CancellationToken ct) => artwork.SetAsync(trackId, request, ct))
            .WithName("setTrackArtwork").RequireGateOpen().WithTags("artwork").Produces<TrackDto>()
            .ProducesProblem(StatusCodes.Status400BadRequest).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status413PayloadTooLarge).ProducesProblem(StatusCodes.Status415UnsupportedMediaType)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapDelete(ArtworkRoute, (Guid trackId, UserArtworkService artwork, CancellationToken ct) => artwork.ResetAsync(trackId, ct))
            .WithName("resetTrackArtwork").RequireGateOpen().WithTags("artwork").Produces<TrackDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
