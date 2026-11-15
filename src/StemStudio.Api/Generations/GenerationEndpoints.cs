using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;

namespace StemStudio.Api.Generations;

/// <summary>Maps the generation routes of backend 1.8.0 (feature 009).</summary>
public static class GenerationEndpoints
{
    /// <summary>Route of one generation.</summary>
    public const string GenerationRoute = ApiRoutes.Generations + "/{generationId:guid}";

    /// <summary>Route of one take.</summary>
    public const string TakeRoute = GenerationRoute + "/takes/{index:int}";

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
        return app.MapGenerationReadEndpoints();
    }

    private static IEndpointRouteBuilder MapGenerationReadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Generations, (string? kind, string? sourceTrackId, GenerationService generations, CancellationToken ct) =>
                generations.ListAsync(kind, sourceTrackId, ct))
            .WithName("listGenerations").WithTags("generation").Produces<List<GenerationDto>>().ProducesValidationProblem();
        app.MapGet(GenerationRoute, (Guid generationId, GenerationService generations, CancellationToken ct) => generations.GetAsync(generationId, ct))
            .WithName("getGeneration").WithTags("generation").Produces<GenerationDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapDelete(GenerationRoute, (Guid generationId, GenerationService generations, CancellationToken ct) => generations.DeleteAsync(generationId, ct))
            .WithName("deleteGeneration").WithTags("generation").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapGenerationActionEndpoints();
    }

    private static IEndpointRouteBuilder MapGenerationActionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(GenerationRoute + "/cancel", (Guid generationId, GenerationService generations, CancellationToken ct) => generations.CancelAsync(generationId, ct))
            .WithName("cancelGeneration").WithTags("generation").Produces<GenerationDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(GenerationRoute + "/retry", (Guid generationId, RetryService retries, CancellationToken ct) => retries.RetryAsync(generationId, ct))
            .WithName("retryGeneration").WithTags("generation").Produces<GenerationDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapGet(TakeRoute + "/audio", (Guid generationId, int index, GenerationService generations, CancellationToken ct) => generations.GetTakeAudioAsync(generationId, index, ct))
            .WithName("getTakeAudio").WithTags("generation").Produces(StatusCodes.Status200OK, contentType: StemService.ContentType)
            .Produces(StatusCodes.Status206PartialContent, contentType: StemService.ContentType).Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound).Produces(StatusCodes.Status416RangeNotSatisfiable);
        return app;
    }
}
