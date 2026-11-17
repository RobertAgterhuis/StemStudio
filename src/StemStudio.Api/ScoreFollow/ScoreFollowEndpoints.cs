using StemStudio.Contracts.ScoreFollow;

namespace StemStudio.Api.ScoreFollow;

/// <summary>Maps the score-follow operations of backend 1.13.0 (tag <c>score-follow</c>).</summary>
public static class ScoreFollowEndpoints
{
    /// <summary>Content-Security-Policy of a page: no scripts, inline styles and the embedded font only.</summary>
    public const string PageSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; font-src data:";

    /// <summary>Cache-Control of a page: the URL carries the view revision.</summary>
    public const string PageCacheControl = "private, max-age=31536000, immutable";

    private const string Tag = "score-follow";
    private const string ViewRoute = "/api/assets/{assetId:guid}/score-view";

    /// <summary>Maps <c>listTrackScores</c>, <c>getScoreView</c>, <c>createScoreView</c> and <c>getScoreViewPage</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapScoreFollowEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/tracks/{trackId:guid}/scores", (Guid trackId, ScoreViewService views, CancellationToken ct) => views.ListAsync(trackId, ct))
            .WithName("listTrackScores").WithTags(Tag).Produces<IReadOnlyList<TrackScoreDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(ViewRoute, (Guid assetId, ScoreViewService views, CancellationToken ct) => views.GetAsync(assetId, ct))
            .WithName("getScoreView").WithTags(Tag).Produces<ScoreViewDto>().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(ViewRoute, (Guid assetId, ScoreViewService views, CancellationToken ct) => views.CreateAsync(assetId, ct))
            .WithName("createScoreView").WithTags(Tag).Produces<ScoreViewDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapGet(ViewRoute + "/pages/{page:int}", PageAsync)
            .WithName("getScoreViewPage").WithTags(Tag).Produces(StatusCodes.Status200OK, contentType: "image/svg+xml")
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    private static async Task<IResult> PageAsync([AsParameters] ScoreViewPageRequest request, CancellationToken ct)
    {
        var result = await request.Views.PageAsync(request.AssetId, request.Page, request.Files, ct);
        var headers = request.Context.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = PageSecurityPolicy;
        if (result is Microsoft.AspNetCore.Http.HttpResults.PhysicalFileHttpResult)
        {
            headers.CacheControl = PageCacheControl;
        }

        return result;
    }
}

/// <summary>Route values and services of <c>getScoreViewPage</c>.</summary>
/// <param name="AssetId">Score item.</param>
/// <param name="Page">1-based page.</param>
/// <param name="Views">The score view service.</param>
/// <param name="Files">Stored pages.</param>
/// <param name="Context">The request.</param>
public sealed record ScoreViewPageRequest(Guid AssetId, int Page, ScoreViewService Views, ScoreViewFiles Files, HttpContext Context);
