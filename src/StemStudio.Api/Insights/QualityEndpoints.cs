using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.Insights;

namespace StemStudio.Api.Insights;

/// <summary>Maps <c>GET</c> and <c>POST /api/tracks/{trackId}/quality</c> (backend 1.19.0, tag <c>insights</c>).</summary>
public static class QualityEndpoints
{
    /// <summary>Maps the quality endpoints.</summary>
    /// <param name="app">Endpoint builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapQualityEndpoints(this IEndpointRouteBuilder app)
    {
        const string route = "/api/tracks/{trackId:guid}/quality";
        app.MapGet(route, (Guid trackId, QualityService quality, CancellationToken ct) => quality.GetAsync(trackId, ct))
            .WithName("getQualityReport").WithTags("insights").Produces<QualityReportDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(route, (Guid trackId, QualityService quality, CancellationToken ct) => quality.RequestAsync(trackId, ct))
            .WithName("requestQualityReport").RequireGateOpen().WithTags("insights").Produces<QualityReportDto>(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status503ServiceUnavailable);
        return app;
    }
}
