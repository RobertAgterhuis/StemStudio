using StemStudio.Contracts;
using StemStudio.Contracts.Styles;
using StemStudio.Api.LibraryCare.Gate;

namespace StemStudio.Api.Styles;

/// <summary>Maps the style library routes of backend 1.8.0 (feature 009).</summary>
public static class StyleEndpoints
{
    /// <summary>Route of one style.</summary>
    public const string StyleRoute = ApiRoutes.Styles + "/{styleId:guid}";

    /// <summary>Maps the style endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapStyleEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.Styles, ([AsParameters] StyleListQuery query, StyleService styles, CancellationToken ct) => styles.ListAsync(query, ct))
            .WithName("listStyles").WithTags("styles").Produces<List<StyleDto>>().ProducesValidationProblem();
        app.MapGet(StyleRoute, (Guid styleId, StyleService styles, CancellationToken ct) => styles.GetAsync(styleId, ct))
            .WithName("getStyle").WithTags("styles").Produces<StyleDto>().ProducesProblem(StatusCodes.Status404NotFound);
        return app.MapStyleWriteEndpoints();
    }

    private static IEndpointRouteBuilder MapStyleWriteEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(ApiRoutes.Styles, (StyleInput? input, StyleEditingService editing, CancellationToken ct) => editing.CreateAsync(input, ct))
            .WithName("createStyle").WithTags("styles").Produces<StyleDto>(StatusCodes.Status201Created)
            .ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPut(StyleRoute, (Guid styleId, StyleInput? input, StyleEditingService editing, CancellationToken ct) => editing.UpdateAsync(styleId, input, ct))
            .WithName("updateStyle").WithTags("styles").Produces<StyleDto>().ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(StyleRoute, (Guid styleId, StyleEditingService editing, CancellationToken ct) => editing.DeleteAsync(styleId, ct))
            .WithName("deleteStyle").RequireGateOpen().WithTags("styles").Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(StyleRoute + "/duplicate", (Guid styleId, StyleEditingService editing, CancellationToken ct) => editing.DuplicateAsync(styleId, ct))
            .WithName("duplicateStyle").WithTags("styles").Produces<StyleDto>(StatusCodes.Status201Created).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }
}
