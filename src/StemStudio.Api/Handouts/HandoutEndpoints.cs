using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;

namespace StemStudio.Api.Handouts;

/// <summary>Maps the handout and practice note endpoints (backend 1.19.0, tag <c>handouts</c>).</summary>
public static class HandoutEndpoints
{
    /// <summary>Maps the endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapHandoutEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/handouts/options", (Guid? trackId, Guid? setlistId, string? family, string? transposition, HandoutOptionsService options, CancellationToken ct) =>
                options.GetAsync(trackId, setlistId, family, transposition, ct))
            .WithName("getHandoutOptions").WithTags("handouts").Produces<HandoutOptionsDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPost(ApiRoutes.Handouts, (CreateHandoutRequest request, HandoutService handouts, CancellationToken ct) => handouts.CreateAsync(request, ct))
            .WithName("createHandout").WithTags("handouts").Produces<AssetDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        const string notes = "/api/tracks/{trackId:guid}/practice-notes";
        app.MapGet(notes, (Guid trackId, PracticeNoteService service, CancellationToken ct) => service.ListAsync(trackId, ct))
            .WithName("getPracticeNotes").WithTags("handouts").Produces<PracticeNotesDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(notes + "/{family}", (Guid trackId, string family, SetPracticeNoteRequest request, PracticeNoteService service, CancellationToken ct) =>
                service.SetAsync(trackId, family, request, ct))
            .WithName("setPracticeNote").WithTags("handouts").Produces<PracticeNoteDto>().Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
