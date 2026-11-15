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
        MapDocument(app);
        MapAlignment(app);
        MapControls(app);
        return app;
    }

    private static void MapControls(IEndpointRouteBuilder app)
    {
        app.MapDelete(LyricsRoute, (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.RemoveAsync(trackId, ct))
            .WithName("removeLyrics").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(LyricsRoute + "/search", (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.SearchAsync(trackId, ct))
            .WithName("searchLyrics").WithTags("lyrics").Produces<LyricsDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }

    private static void MapDocument(IEndpointRouteBuilder app)
    {
        app.MapGet(LyricsRoute, (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.GetAsync(trackId, ct))
            .WithName("getLyrics").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(LyricsRoute, (Guid trackId, SetLyricsRequest request, LyricsService lyrics, CancellationToken ct) => lyrics.SetAsync(trackId, request, ct))
            .WithName("setLyrics").WithTags("lyrics").Produces<LyricsDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPut(LyricsRoute + "/offset", (Guid trackId, LyricsOffsetRequest request, LyricsService lyrics, CancellationToken ct) => lyrics.SetOffsetAsync(trackId, request, ct))
            .WithName("setLyricsOffset").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
    }

    private static void MapAlignment(IEndpointRouteBuilder app)
    {
        app.MapPost(LyricsRoute + "/align", (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.AlignAsync(trackId, ct))
            .WithName("alignLyrics").WithTags("lyrics").Produces<LyricsDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(LyricsRoute + "/align/cancel", (Guid trackId, LyricsService lyrics, CancellationToken ct) => lyrics.CancelAlignAsync(trackId, ct))
            .WithName("cancelLyricsAlignment").WithTags("lyrics").Produces<LyricsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
    }
}
