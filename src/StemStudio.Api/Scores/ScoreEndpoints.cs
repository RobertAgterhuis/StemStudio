using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;

namespace StemStudio.Api.Scores;

/// <summary>Maps <c>GET /api/scores/options</c> and <c>POST /api/assets/scores</c> (backend 1.7.0, feature 008).</summary>
public static class ScoreEndpoints
{
    /// <summary>Maps the score endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapScoreEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.ScoreOptions, (Guid? trackId, string? channel, Guid? assetId, ScoreOptionsService options, CancellationToken ct) => options.GetAsync(trackId, channel, assetId, ct))
            .WithName("getScoreOptions").WithTags("scores").Produces<ScoreOptionsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPost(ApiRoutes.Scores, (CreateScoreRequest request, ScoreService scores, CancellationToken ct) => scores.CreateAsync(request, ct))
            .WithName("createScore").WithTags("scores").Produces<AssetDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
