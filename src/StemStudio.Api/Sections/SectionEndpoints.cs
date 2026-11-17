using StemStudio.Contracts.Sections;

namespace StemStudio.Api.SongSections;

/// <summary>Maps the section operations of backend 1.13.0 (tag <c>sections</c>).</summary>
public static class SectionEndpoints
{
    private const string Tag = "sections";
    private const string Route = "/api/tracks/{trackId:guid}/sections";

    /// <summary>Maps <c>getSections</c>, <c>replaceSections</c> and <c>detectSections</c>.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapSectionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(Route, (Guid trackId, SectionService sections, CancellationToken ct) => sections.GetAsync(trackId, ct))
            .WithName("getSections").WithTags(Tag).Produces<SectionSetDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(Route, (Guid trackId, SectionsUpdateRequest request, SectionService sections, CancellationToken ct) => sections.ReplaceAsync(trackId, request, ct))
            .WithName("replaceSections").WithTags(Tag).Produces<SectionSetDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPost(Route + "/detect", (Guid trackId, DetectSectionsRequest? request, SectionService sections, CancellationToken ct) =>
                sections.DetectAsync(trackId, request?.Overwrite ?? false, ct))
            .WithName("detectSections").WithTags(Tag).Produces<SectionSetDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
