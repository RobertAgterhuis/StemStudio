using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;

namespace StemStudio.Api.Mixes;

/// <summary>Maps the <c>/api/mixes</c> endpoints.</summary>
public static class MixEndpoints
{
    /// <summary>Route of a single mix with a GUID constraint.</summary>
    public const string MixRoute = ApiRoutes.Mixes + "/{mixId:guid}";

    /// <summary>Maps the mix endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapMixEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Mixes, (string? trackId, MixService mixes, CancellationToken ct) => mixes.ListAsync(trackId, ct))
            .WithName("listMixes").WithTags("mixes").Produces<List<MixSummaryDto>>().ProducesValidationProblem();
        app.MapPost(ApiRoutes.Mixes, (CreateMixRequest? request, MixService mixes, CancellationToken ct) => mixes.CreateAsync(request, ct))
            .WithName("createMix").WithTags("mixes").Produces<MixDto>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapGet(MixRoute, (Guid mixId, MixService mixes, CancellationToken ct) => mixes.GetAsync(mixId, ct))
            .WithName("getMix").WithTags("mixes").Produces<MixDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(MixRoute, (Guid mixId, MixSettingsDto? settings, MixService mixes, CancellationToken ct) => mixes.UpdateAsync(mixId, settings, ct))
            .WithName("updateMix").WithTags("mixes").Produces<MixDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(MixRoute, (Guid mixId, MixService mixes, CancellationToken ct) => mixes.DeleteAsync(mixId, ct))
            .WithName("deleteMix").WithTags("mixes").Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(MixRoute + "/duplicate", (Guid mixId, MixService mixes, CancellationToken ct) => mixes.DuplicateAsync(mixId, ct))
            .WithName("duplicateMix").WithTags("mixes").Produces<MixDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
