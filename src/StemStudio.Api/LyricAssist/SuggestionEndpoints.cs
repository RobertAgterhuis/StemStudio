using System.Text.Json;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Api.LyricAssist;

/// <summary>Route and query of <c>GET /api/lyrics/{lyricsId}/suggestions</c>.</summary>
/// <param name="LyricsId">Sheet id.</param>
/// <param name="Status">Status filter (contract name).</param>
/// <param name="Category">Category filter (contract name).</param>
public sealed record SuggestionQuery(Guid LyricsId, string? Status, string? Category);

/// <summary>Maps the suggestion, mark, sheet-settings, undo, revision and word routes of backend 1.23.0.</summary>
public static class SuggestionEndpoints
{
    private const string Tag = LyricAssistEndpoints.Tag;
    private const string SheetRoute = ApiRoutes.LyricsSheets + "/{lyricsId:guid}";

    /// <summary>Maps the routes.</summary>
    /// <param name="app">Route builder.</param>
    /// <returns>The builder.</returns>
    public static IEndpointRouteBuilder MapSuggestionEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(SheetRoute + "/suggestions", ([AsParameters] SuggestionQuery query, SuggestionService service, CancellationToken ct) => ListAsync(query, service, ct)).WithName("listLyricSuggestions").WithTags(Tag).Produces<SuggestionListDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(SheetRoute + "/suggestions/accept", (Guid lyricsId, SuggestionDecisionRequest? request, SuggestionService service, CancellationToken ct) => service.AcceptAsync(lyricsId, request, ct))
            .WithName("acceptLyricSuggestions").WithTags(Tag).Produces<AcceptResultDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        app.MapPost(SheetRoute + "/suggestions/reject", (Guid lyricsId, SuggestionDecisionRequest? request, SuggestionService service, CancellationToken ct) => service.RejectAsync(lyricsId, request, ct))
            .WithName("rejectLyricSuggestions").WithTags(Tag).Produces<SuggestionListDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapPost(SheetRoute + "/suggestions/{suggestionId:guid}/intentional",
                (Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest? request, SuggestionService service, CancellationToken ct) => service.MarkIntentionalAsync(lyricsId, suggestionId, request, ct))
            .WithName("markSuggestionIntentional").WithTags(Tag).Produces<IntentionalMarkDto>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app.MapMarkEndpoints().MapHistoryEndpoints().MapWordEndpoints();
    }

    private static IEndpointRouteBuilder MapMarkEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(SheetRoute + "/intentional-marks", (Guid lyricsId, AssistPreferencesService service, CancellationToken ct) => service.ListMarksAsync(lyricsId, ct))
            .WithName("listIntentionalMarks").WithTags(Tag).Produces<List<IntentionalMarkDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(SheetRoute + "/intentional-marks", (Guid lyricsId, IntentionalMarkInput? input, AssistPreferencesService service, CancellationToken ct) => service.CreateMarkAsync(lyricsId, input, ct))
            .WithName("createIntentionalMark").WithTags(Tag).Produces<IntentionalMarkDto>(StatusCodes.Status201Created).ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(SheetRoute + "/intentional-marks/{markId:guid}", (Guid lyricsId, Guid markId, AssistPreferencesService service, CancellationToken ct) => service.DeleteMarkAsync(lyricsId, markId, ct))
            .WithName("deleteIntentionalMark").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPut(SheetRoute + "/assist-settings", (Guid lyricsId, SheetAssistSettingsUpdate? update, AssistPreferencesService service, CancellationToken ct) => service.UpdateSheetSettingsAsync(lyricsId, update, ct))
            .WithName("updateSheetAssistSettings").WithTags(Tag).Produces<SheetAssistInfoDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    private static IEndpointRouteBuilder MapHistoryEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(SheetRoute + "/undo", (Guid lyricsId, RevisionRequest? request, LyricsRevisionService service, CancellationToken ct) => service.UndoAsync(lyricsId, request, ct))
            .WithName("undoLyricSuggestions").WithTags(Tag).Produces<AcceptResultDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        app.MapGet(SheetRoute + "/revisions", (Guid lyricsId, LyricsRevisionService service, CancellationToken ct) => service.ListAsync(lyricsId, ct))
            .WithName("listLyricsRevisions").WithTags(Tag).Produces<List<LyricsRevisionSummaryDto>>().ProducesProblem(StatusCodes.Status404NotFound);
        app.MapPost(SheetRoute + "/revisions/{revision:int:min(1)}/restore",
                (Guid lyricsId, int revision, RevisionRequest? request, LyricsRevisionService service, CancellationToken ct) => service.RestoreAsync(lyricsId, revision, request, ct))
            .WithName("restoreLyricsRevision").WithTags(Tag).Produces<AcceptResultDto>().ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict);
        return app;
    }

    private static IEndpointRouteBuilder MapWordEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet(ApiRoutes.LyricAssistWords, (string? language, AssistPreferencesService service, CancellationToken ct) =>
                TryParse(language, out AssistLanguageDto parsed) ? service.ListWordsAsync(parsed, ct) : service.ListWordsAsync(null, ct))
            .WithName("listLyricWords").WithTags(Tag).Produces<List<LyricWordDto>>().ProducesValidationProblem();
        app.MapPost(ApiRoutes.LyricAssistWords, (LyricWordInput? input, AssistPreferencesService service, CancellationToken ct) => service.AddWordAsync(input, ct))
            .WithName("addLyricWord").WithTags(Tag).Produces<LyricWordDto>(StatusCodes.Status201Created).ProducesValidationProblem().ProducesProblem(StatusCodes.Status409Conflict);
        app.MapDelete(ApiRoutes.LyricAssistWords + "/{wordId:guid}", (Guid wordId, AssistPreferencesService service, CancellationToken ct) => service.DeleteWordAsync(wordId, ct))
            .WithName("deleteLyricWord").WithTags(Tag).Produces(StatusCodes.Status204NoContent).ProducesProblem(StatusCodes.Status404NotFound);
        return app;
    }

    /// <summary>Lists suggestions with the contract's enum names in the query.</summary>
    /// <param name="query">Sheet id and filters.</param>
    /// <param name="service">Service.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200, 400 or 404.</returns>
    public static Task<IResult> ListAsync(SuggestionQuery query, SuggestionService service, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(service);
        var (lyricsId, status, category) = (query.LyricsId, query.Status, query.Category);
        if (!Optional(status, out SuggestionStatusDto? statusValue) || !Optional(category, out SuggestionCategoryDto? categoryValue))
        {
            return Task.FromResult(ApiProblems.Validation(status is not null && !TryParse(status, out SuggestionStatusDto _) ? "status" : "category", "Unknown filter value."));
        }

        return service.ListAsync(lyricsId, statusValue, categoryValue, cancellationToken);
    }

    /// <summary>Parses a contract enum name (e.g. <c>open</c>, <c>nl</c>).</summary>
    /// <typeparam name="T">Enum.</typeparam>
    /// <param name="text">Text.</param>
    /// <param name="value">The value.</param>
    /// <returns>True when known.</returns>
    public static bool TryParse<T>(string? text, out T value)
        where T : struct, Enum
    {
        value = default;
        if (string.IsNullOrEmpty(text) || text.Length > 40 || !text.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '.'))
        {
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize<T>($"\"{text}\"");
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool Optional<T>(string? text, out T? value)
        where T : struct, Enum
    {
        value = null;
        if (text is null)
        {
            return true;
        }

        var ok = TryParse(text, out T parsed);
        value = ok ? parsed : null;
        return ok;
    }
}
