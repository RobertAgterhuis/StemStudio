using StemStudio.Contracts;
using StemStudio.Contracts.Styles;

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
        return app;
    }
}
