using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Api.Tracks;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Tracks;

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
        app.MapGet(ApiRoutes.GenerationStatus, (GenerationStatusService status, CancellationToken ct) => status.GetAsync(ct))
            .WithName("getGenerationStatus").WithTags("generation").Produces<GenerationStatusDto>();
        app.MapPost(TrackEndpoints.TrackRoute + "/restyles", (Guid trackId, RestyleRequest? request, RestyleService restyles, CancellationToken ct) =>
                restyles.CreateAsync(trackId, request, ct))
            .WithName("createRestyle").WithTags("generation").Produces<GenerationDto>(StatusCodes.Status202Accepted)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(ApiRoutes.Songs, (SongRequest? request, SongService songs, CancellationToken ct) => songs.CreateAsync(request, ct))
            .WithName("createSong").WithTags("songs").Produces<GenerationDto>(StatusCodes.Status202Accepted).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
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
            .WithName("deleteGeneration").RequireGateOpen().WithTags("generation").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
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
        app.MapGet(TakeRoute + "/vocal", (Guid generationId, int index, GenerationService generations, CancellationToken ct) => generations.GetTakeVocalAsync(generationId, index, ct))
            .WithName("getTakeVocal").WithTags("voices").Produces(StatusCodes.Status200OK, contentType: StemService.ContentType)
            .Produces(StatusCodes.Status206PartialContent, contentType: StemService.ContentType).Produces(StatusCodes.Status304NotModified)
            .ProducesProblem(StatusCodes.Status404NotFound).Produces(StatusCodes.Status416RangeNotSatisfiable);
        app.MapPost(TakeRoute + "/voice-conversions", (Guid generationId, int index, VoiceConversionRequest? request, Voices.VoiceConversionService conversions, CancellationToken ct) =>
                conversions.CreateAsync(generationId, index, request, ct))
            .WithName("createVoiceConversion").WithTags("voices").Produces<GenerationDto>(StatusCodes.Status202Accepted).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        app.MapPost(TakeRoute + "/import", (Guid generationId, int index, TakeImporter importer, CancellationToken ct) => importer.ImportAsync(generationId, index, ct))
            .WithName("importTake").WithTags("generation").Produces<TrackDto>().Produces<TrackDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
