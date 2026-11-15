using StemStudio.Api.Tracks;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Generations;

/// <summary>Maps the generation routes of backend 1.8.0 (feature 009).</summary>
public static class GenerationEndpoints
{
    /// <summary>Maps the generation endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(TrackEndpoints.TrackRoute + "/restyles", (Guid trackId, RestyleRequest? request, RestyleService restyles, CancellationToken ct) =>
                restyles.CreateAsync(trackId, request, ct))
            .WithName("createRestyle").WithTags("generation").Produces<GenerationDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
