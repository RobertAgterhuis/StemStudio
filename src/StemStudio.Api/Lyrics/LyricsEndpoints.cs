using StemStudio.Api.Tracks;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Lyrics;

/// <summary>Maps <c>/api/tracks/{trackId}/lyrics/**</c> (backend 1.6.0, feature 006); every answer is <c>no-store</c>.</summary>
public static class LyricsEndpoints
{
    private const string LyricsRoute = TrackEndpoints.TrackRoute + "/lyrics";

    /// <summary>Maps the lyrics endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapLyricsEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(LyricsRoute, (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.GetAsync(trackId, ct))
            .WithName("getLyrics").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(LyricsRoute + "/offset", (Guid trackId, LyricsOffsetRequest request, LyricsService lyrics, CancellationToken ct) => lyrics.SetOffsetAsync(trackId, request, ct))
            .WithName("setLyricsOffset").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
