using StemStudio.Api.Tracks;

namespace StemStudio.Api.Artwork;

/// <summary>Maps the artwork routes of backend 1.4.0 (feature 005).</summary>
public static class ArtworkEndpoints
{
    /// <summary>Route of the picture resource of a track.</summary>
    public const string ArtworkRoute = TrackEndpoints.TrackRoute + "/artwork";

    /// <summary>Maps <c>GET /api/tracks/{trackId}/artwork/{variant}</c>.</summary>
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
        return app;
    }
}
