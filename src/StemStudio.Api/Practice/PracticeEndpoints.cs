using StemStudio.Contracts;
using StemStudio.Contracts.Practice;

namespace StemStudio.Api.Practice;

/// <summary>Maps the practice log operations of backend 1.13.0 (tag <c>practice</c>).</summary>
public static class PracticeEndpoints
{
    private const string Tag = "practice";
    private const string SessionRoute = ApiRoutes.PracticeSessions + "/{sessionId:guid}";

    /// <summary>Maps settings, sessions, summary and overview.</summary>
    /// <param name="app">The route builder.</param>
    /// <returns>The same route builder.</returns>
    public static IEndpointRouteBuilder MapPracticeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.PracticeLogSettings, (PracticeService log, CancellationToken ct) => log.GetSettingsAsync(ct))
            .WithName("getPracticeLogSettings").WithTags(Tag).Produces<PracticeLogSettingsDto>();
        app.MapPut(ApiRoutes.PracticeLogSettings, (PracticeLogSettingsUpdateRequest request, PracticeService log, CancellationToken ct) => log.UpdateSettingsAsync(request, ct))
            .WithName("updatePracticeLogSettings").WithTags(Tag).Produces<PracticeLogSettingsDto>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(ApiRoutes.PracticeSessions, (Guid? trackId, DateTimeOffset? before, int? limit, PracticeService log, CancellationToken ct) => log.ListAsync(trackId, before, limit, ct))
            .WithName("listPracticeSessions").WithTags(Tag).Produces<IReadOnlyList<PracticeSessionDto>>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(ApiRoutes.PracticeSessions, (Guid? trackId, PracticeService log, CancellationToken ct) => log.DeleteManyAsync(trackId, ct))
            .WithName("deletePracticeSessions").WithTags(Tag).Produces<DeletedCountDto>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPut(SessionRoute, (Guid sessionId, PracticeSessionInputDto request, PracticeService log, CancellationToken ct) => log.UpsertAsync(sessionId, request, ct))
            .WithName("upsertPracticeSession").WithTags(Tag).Produces<PracticeSessionDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapDelete(SessionRoute, (Guid sessionId, PracticeService log, CancellationToken ct) => log.DeleteAsync(sessionId, ct))
            .WithName("deletePracticeSession").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(ApiRoutes.PracticeSummary, (Guid? trackId, int? days, int? utcOffsetMinutes, PracticeService log, CancellationToken ct) => log.SummaryAsync(trackId, days, utcOffsetMinutes, ct))
            .WithName("getPracticeSummary").WithTags(Tag).Produces<PracticeSummaryDto>().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapGet(ApiRoutes.PracticeOverview, (int? utcOffsetMinutes, PracticeService log, CancellationToken ct) => log.OverviewAsync(utcOffsetMinutes, ct))
            .WithName("getPracticeOverview").WithTags(Tag).Produces<IReadOnlyList<PracticeTrackTotalDto>>().ProducesValidationProblem(StatusCodes.Status422UnprocessableEntity);
        return app;
    }
}
