using StemStudio.Agent.Ports;
using StemStudio.Contracts;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Agent.Loopback;

/// <summary>
/// The eight lyric tools of catalogue 1.2.0 over the public endpoints (feature 023, T057). Every result is projected to the text-free
/// summaries of <see cref="ILyricAssistActions"/> (FR-019): no sheet, suggestion, proposal or explanation text reaches the agent.
/// </summary>
/// <param name="client">Loopback client.</param>
public sealed class LoopbackLyricAssistActions(LoopbackClient client) : ILyricAssistActions
{
    /// <inheritdoc />
    public async Task<LyricSheetState> GetSheetStateAsync(Guid lyricsId, CancellationToken cancellationToken)
    {
        var sheet = await client.GetAsync<LyricsSheetDto>(ApiRoutes.LyricsSheet(lyricsId), cancellationToken);
        return new LyricSheetState { LyricsId = sheet.Id, Revision = sheet.Revision, Language = sheet.Language, Title = sheet.Title };
    }

    /// <inheritdoc />
    public async Task<LyricAssistSummaryDto> StartAssistAsync(Guid lyricsId, CreateLyricAssistRequest request, CancellationToken cancellationToken) =>
        Summary(await client.SendAsync<LyricAssistDto>(HttpMethod.Post, ApiRoutes.LyricsAssists(lyricsId), request, cancellationToken));

    /// <inheritdoc />
    public async Task<LyricAssistSummaryDto> GetAssistAsync(Guid assistId, CancellationToken cancellationToken) =>
        Summary(await client.GetAsync<LyricAssistDto>(ApiRoutes.LyricAssistById(assistId), cancellationToken));

    /// <inheritdoc />
    public async Task<LyricSuggestionsSummary> ListSuggestionsAsync(Guid lyricsId, SuggestionStatusDto? status, SuggestionCategoryDto? category, CancellationToken cancellationToken)
    {
        var list = await client.GetAsync<SuggestionListDto>(ApiRoutes.LyricsSuggestions(lyricsId, status, category), cancellationToken);
        return new LyricSuggestionsSummary { Revision = list.Revision, Items = list.Suggestions.Select(Ref).ToList() };
    }

    /// <inheritdoc />
    public async Task<LyricAcceptSummary> AcceptAsync(Guid lyricsId, SuggestionDecisionRequest request, CancellationToken cancellationToken) =>
        Accept(await client.SendAsync<AcceptResultDto>(HttpMethod.Post, ApiRoutes.LyricsSuggestionsAccept(lyricsId), request, cancellationToken));

    /// <inheritdoc />
    public async Task<LyricAcceptSummary> UndoAsync(Guid lyricsId, int expectedRevision, CancellationToken cancellationToken) =>
        Accept(await client.SendAsync<AcceptResultDto>(HttpMethod.Post, ApiRoutes.LyricsUndo(lyricsId), new RevisionRequest { ExpectedRevision = expectedRevision }, cancellationToken));

    /// <inheritdoc />
    public async Task<IntentionalMarkRef> MarkIntentionalAsync(Guid lyricsId, Guid suggestionId, MarkSuggestionIntentionalRequest request, CancellationToken cancellationToken)
    {
        var mark = await client.SendAsync<IntentionalMarkDto>(HttpMethod.Post, ApiRoutes.LyricsSuggestionIntentional(lyricsId, suggestionId), request, cancellationToken);
        return new IntentionalMarkRef { MarkId = mark.MarkId, Scope = mark.Scope };
    }

    /// <inheritdoc />
    public Task DeleteIntentionalMarkAsync(Guid lyricsId, Guid markId, CancellationToken cancellationToken) =>
        client.SendAsync(HttpMethod.Delete, ApiRoutes.LyricsIntentionalMark(lyricsId, markId), null, cancellationToken);

    /// <inheritdoc />
    public Task<RhymeResultDto> FindRhymesAsync(string word, AssistLanguageDto language, int? limit, CancellationToken cancellationToken) =>
        client.GetAsync<RhymeResultDto>(ApiRoutes.LyricAssistRhymesFor(word, language, limit), cancellationToken);

    /// <inheritdoc />
    public Task<SyllableReportDto> CountSyllablesAsync(Guid lyricsId, CancellationToken cancellationToken) =>
        client.GetAsync<SyllableReportDto>(ApiRoutes.LyricsSyllables(lyricsId), cancellationToken);

    private static LyricAssistSummaryDto Summary(LyricAssistDto assist) => new()
    {
        AssistId = assist.AssistId,
        LyricsId = assist.LyricsId,
        SheetRevision = assist.SheetRevision,
        Kind = assist.Kind,
        Status = assist.Status,
        Language = assist.Language,
        Sensitivity = assist.Sensitivity,
        Layers = assist.Layers,
        Backend = assist.Backend,
        Model = assist.Model,
        UsingFallback = assist.UsingFallback,
        BlocksTotal = assist.BlocksTotal,
        BlocksDone = assist.BlocksDone,
        BlocksSkipped = assist.BlocksSkipped,
        Counts = assist.Counts,
        Discarded = assist.Discarded,
        Usage = assist.Usage,
        Origin = assist.Origin,
        AgentRunId = assist.AgentRunId,
        Failure = assist.Failure,
        EstimatedSeconds = assist.EstimatedSeconds,
        CreatedAt = assist.CreatedAt,
        StartedAt = assist.StartedAt,
        EndedAt = assist.EndedAt,
    };

    private static LyricSuggestionRef Ref(LyricSuggestionDto suggestion) => new()
    {
        SuggestionId = suggestion.SuggestionId,
        Category = suggestion.Category,
        Type = suggestion.Type,
        Severity = suggestion.Severity,
        Line = suggestion.Anchor.Line,
        Status = suggestion.Status,
        Source = suggestion.Source,
    };

    private static LyricAcceptSummary Accept(AcceptResultDto result) => new() { Revision = result.Revision, Applied = result.Applied.Count, Stale = result.Stale.Count };
}
