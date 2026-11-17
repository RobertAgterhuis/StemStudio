using System.Globalization;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>Maps the assist routes of backend 1.23.0 (tag <c>lyric-assist</c>).</summary>
public static class LyricAssistEndpoints
{
    /// <summary>Tag of every 023 operation.</summary>
    public const string Tag = "lyric-assist";

    private const string SheetRoute = ApiRoutes.LyricsSheets + "/{lyricsId:guid}";
    private const string AssistRoute = ApiRoutes.LyricAssists + "/{assistId:guid}";

    /// <summary>Maps status, settings, create, list, get, events and cancel.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapAssistEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LyricAssistStatus, (LyricAssistStatusService service, CancellationToken ct) => service.StatusAsync(ct))
            .WithName("getLyricAssistStatus").WithTags(Tag).Produces<LyricAssistStatusDto>();
        app.MapGet(ApiRoutes.LyricAssistSettings, (LyricAssistStatusService service, CancellationToken ct) => service.SettingsAsync(ct))
            .WithName("getLyricAssistSettings").WithTags(Tag).Produces<LyricAssistSettingsDto>();
        app.MapPut(ApiRoutes.LyricAssistSettings, (LyricAssistSettingsUpdate? update, LyricAssistStatusService service, CancellationToken ct) => service.UpdateSettingsAsync(update, ct))
            .WithName("updateLyricAssistSettings").WithTags(Tag).Produces<LyricAssistSettingsDto>().ProducesValidationProblem();
        app.MapPost(SheetRoute + "/assists", (Guid lyricsId, CreateLyricAssistRequest? request, LyricAssistService service, CancellationToken ct) =>
                service.CreateAsync(lyricsId, request, ct))
            .WithName("createLyricAssist").WithTags(Tag).Produces<LyricAssistDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapGet(SheetRoute + "/assists", (Guid lyricsId, LyricAssistService service, CancellationToken ct) => service.ListAsync(lyricsId, ct))
            .WithName("listLyricAssists").WithTags(Tag).Produces<List<LyricAssistSummaryDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(AssistRoute, (Guid assistId, LyricAssistService service, CancellationToken ct) => service.GetAsync(assistId, ct))
            .WithName("getLyricAssist").WithTags(Tag).Produces<LyricAssistDto>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapGet(AssistRoute + "/events", StreamAsync)
            .WithName("streamLyricAssistEvents").WithTags(Tag).Produces(StatusCodes.Status200OK, contentType: "text/event-stream").ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(AssistRoute + "/cancel", (Guid assistId, LyricAssistService service, CancellationToken ct) => service.CancelAsync(assistId, ct))
            .WithName("cancelLyricAssist").WithTags(Tag).Produces<LyricAssistDto>(StatusCodes.Status202Accepted).ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    /// <summary>Streams the events of an assist.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="context">HTTP context (<c>after</c>, <c>Last-Event-ID</c>).</param>
    /// <param name="assists">Assists.</param>
    /// <param name="stream">The stream writer.</param>
    /// <returns>400, 404, or an empty result after the stream.</returns>
    public static async Task<IResult> StreamAsync(Guid assistId, HttpContext context, ILyricAssistRepository assists, LyricAssistEventWriter stream)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(assists);
        ArgumentNullException.ThrowIfNull(stream);
        if (!TryPosition(context.Request, out var after))
        {
            return ApiProblems.Validation("after", "after and Last-Event-ID must be whole numbers of 0 or more.");
        }

        if (await assists.FindAsync(assistId, context.RequestAborted) is null)
        {
            return ApiProblems.NotFound($"Assist {assistId} does not exist.");
        }

        await stream.StreamAsync(context.Response, assistId, after, context.RequestAborted);
        return Results.Empty;
    }

    private static bool TryPosition(HttpRequest request, out int after)
    {
        var text = request.Query.TryGetValue("after", out var query) ? query.ToString() : request.Headers["Last-Event-ID"].ToString();
        after = 0;
        return text.Length == 0 || int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out after);
    }
}
