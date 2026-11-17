using StemStudio.Api.Styles;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;

namespace StemStudio.Api.LyricAssist;

/// <summary>The services of <see cref="LyricAssistService"/> (parameter object).</summary>
public sealed record LyricAssistServiceDeps
{
    /// <summary>Stores.</summary>
    public required LyricAssistStores Stores { get; init; }

    /// <summary>Model layer.</summary>
    public required LyricModelLayer ModelLayer { get; init; }

    /// <summary>Own-lyrics corpus.</summary>
    public required LyricsCorpusProvider Corpus { get; init; }

    /// <summary>Imitation guard with the library artists.</summary>
    public required LibraryStyleGuard Imitation { get; init; }

    /// <summary>Event log.</summary>
    public required AssistEventLog Events { get; init; }

    /// <summary>Background runner.</summary>
    public required LyricAssistRunner Runner { get; init; }

    /// <summary>Clock.</summary>
    public required TimeProvider Clock { get; init; }
}

/// <summary>
/// Assists (backend 1.23.0): create with the refusal order of the contract (404, 400, revision, language, own lyrics, imitation, active,
/// model, Claude notice), get, list (newest 20) and cancel. Nothing runs without an explicit request (FR-002).
/// </summary>
/// <param name="deps">Services.</param>
public sealed class LyricAssistService(LyricAssistServiceDeps deps)
{
    private LyricAssistStores Stores => deps.Stores;

    private DateTime Now => deps.Clock.GetUtcNow().UtcDateTime;

    /// <summary>Starts an assist.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="request">The request.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202 or a problem.</returns>
    public async Task<IResult> CreateAsync(Guid lyricsId, CreateLyricAssistRequest? request, CancellationToken cancellationToken)
    {
        if (await Stores.Sheets.FindAsync(lyricsId, cancellationToken) is not { } sheet)
        {
            return NotFound(lyricsId);
        }

        if (request is null)
        {
            return ApiProblems.Validation("kind", "Send the kind of help and the expected revision.");
        }

        var refusal = await RefusalAsync(sheet, request, cancellationToken);
        return refusal ?? await StartAsync(sheet, AssistRequestRules.ToDomain(request, sheet), cancellationToken);
    }

    /// <summary>One assist with its suggestions.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> GetAsync(Guid assistId, CancellationToken cancellationToken) =>
        await Stores.Assists.FindAsync(assistId, cancellationToken) is { } assist
            ? TypedResults.Ok(LyricAssistMapper.ToDto(assist, await Stores.Suggestions.ForAssistAsync(assistId, cancellationToken)))
            : ApiProblems.NotFound($"Assist {assistId} does not exist.");

    /// <summary>The newest 20 assists of a sheet.</summary>
    /// <param name="lyricsId">Sheet id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>200 or 404.</returns>
    public async Task<IResult> ListAsync(Guid lyricsId, CancellationToken cancellationToken) =>
        await Stores.Sheets.FindAsync(lyricsId, cancellationToken) is null
            ? NotFound(lyricsId)
            : TypedResults.Ok((await Stores.Assists.ListForSheetAsync(lyricsId, LyricAssistLimits.MaxAssistsPerSheet, cancellationToken)).Select(LyricAssistMapper.ToSummary).ToList());

    /// <summary>Cancels an assist.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>202, 404 or 409 <c>assist-ended</c>.</returns>
    public async Task<IResult> CancelAsync(Guid assistId, CancellationToken cancellationToken)
    {
        if (await Stores.Assists.FindAsync(assistId, cancellationToken) is not { } assist)
        {
            return ApiProblems.NotFound($"Assist {assistId} does not exist.");
        }

        if (assist.Status.IsTerminal())
        {
            return LyricAssistProblems.AssistEnded();
        }

        if (!await deps.Runner.CancelAsync(assistId))
        {
            assist.Cancel(Now);
            await deps.Events.AppendAsync(assistId, LyricAssistEventTypes.Cancelled, new LyricAssistCancelledEventDto { AssistId = assistId }, cancellationToken);
        }

        return TypedResults.Accepted(Contracts.ApiRoutes.LyricAssistById(assistId), LyricAssistMapper.ToDto(await FreshAsync(assistId, cancellationToken), await Stores.Suggestions.ForAssistAsync(assistId, cancellationToken)));
    }

    private async Task<IResult?> RefusalAsync(LyricsSheet sheet, CreateLyricAssistRequest request, CancellationToken cancellationToken)
    {
        var errors = AssistRequestRules.Validate(request, sheet);
        if (errors.Count > 0)
        {
            return ApiProblems.Validation(errors, string.Empty);
        }

        return SheetRefusal(sheet, request)
            ?? await OwnLyricsRefusalAsync(sheet, cancellationToken)
            ?? await ImitationRefusalAsync(request.Instruction, cancellationToken)
            ?? await ActiveRefusalAsync(cancellationToken)
            ?? await ModelRefusalAsync(sheet, request, cancellationToken);
    }

    private static IResult? SheetRefusal(LyricsSheet sheet, CreateLyricAssistRequest request) =>
        request.ExpectedRevision != sheet.Revision ? LyricAssistProblems.RevisionConflict(sheet.Revision)
        : sheet.AssistLanguage is null ? LyricAssistProblems.LanguageUnsupported()
        : null;

    private async Task<IResult?> OwnLyricsRefusalAsync(LyricsSheet sheet, CancellationToken cancellationToken)
    {
        var corpus = await deps.Corpus.RefreshAsync(cancellationToken);
        return VerbatimGuard.RefusingTrack(sheet.Text, corpus, sheet.OwnWorkOverride?.MatchedTrackId) is { } track
            ? LyricAssistProblems.NotOwnLyrics(track.TrackId, track.Title)
            : null;
    }

    private async Task<IResult?> ImitationRefusalAsync(string? instruction, CancellationToken cancellationToken) =>
        !string.IsNullOrWhiteSpace(instruction) && (await deps.Imitation.GuardAsync(cancellationToken)).IsImitation(instruction) ? LyricAssistProblems.Imitation() : null;

    private async Task<IResult?> ActiveRefusalAsync(CancellationToken cancellationToken) =>
        await Stores.Assists.ActiveAsync(cancellationToken) is [var active, ..] ? LyricAssistProblems.AssistActive(active.Id) : null;

    private async Task<IResult?> ModelRefusalAsync(LyricsSheet sheet, CreateLyricAssistRequest request, CancellationToken cancellationToken)
    {
        var model = await deps.ModelLayer.ResolveAsync(sheet.Title, LyricAssistMapper.Map<AssistKind>(request.Kind), cancellationToken);
        if (!model.Available)
        {
            return request.Kind == AssistKindDto.Check ? null : LyricAssistProblems.ModelUnavailable(model.Reason, model.Fix);
        }

        return model.Backend == AgentBackendDto.Claude && !(await Stores.Words.SettingsAsync(cancellationToken)).HasAcknowledged(LyricAssistLimits.ClaudeNoticeVersion)
            ? LyricAssistProblems.ClaudeNoticeRequired()
            : null;
    }

    private async Task<IResult> StartAsync(LyricsSheet sheet, AssistRequest request, CancellationToken cancellationToken)
    {
        var assist = AssistRun.Create(Guid.NewGuid(), sheet, request, Now);
        await Stores.Assists.AddAsync(assist, cancellationToken);
        await Stores.Assists.PruneAsync(sheet.Id, LyricAssistLimits.MaxAssistsPerSheet, cancellationToken);
        deps.Runner.Start(assist.Id);
        return TypedResults.Accepted(Contracts.ApiRoutes.LyricAssistById(assist.Id), LyricAssistMapper.ToDto(assist, []));
    }

    private async Task<AssistRun> FreshAsync(Guid assistId, CancellationToken cancellationToken)
    {
        var assist = (await Stores.Assists.FindAsync(assistId, cancellationToken))!;
        await Stores.Assists.ReloadAsync(assist, cancellationToken);
        return assist;
    }

    private static IResult NotFound(Guid lyricsId) => ApiProblems.NotFound($"Lyrics {lyricsId} do not exist.");
}
