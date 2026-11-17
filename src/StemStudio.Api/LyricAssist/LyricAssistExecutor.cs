using StemStudio.Agent.LyricAssist;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>The stores an assist run uses (parameter object of <see cref="LyricAssistExecutor"/>).</summary>
public sealed record LyricAssistStores
{
    /// <summary>Sheets.</summary>
    public required ILyricsSheetRepository Sheets { get; init; }

    /// <summary>Assists.</summary>
    public required ILyricAssistRepository Assists { get; init; }

    /// <summary>Suggestions.</summary>
    public required ILyricSuggestionRepository Suggestions { get; init; }

    /// <summary>Personal words.</summary>
    public required ILyricWordRepository Words { get; init; }
}

/// <summary>
/// Runs one assist (FR-002, FR-004, FR-027, T028): waits while the library is in maintenance, runs the dictionary layer at once, then
/// the model layer block by block through the ai-agent assistant, and ends the assist completed, failed or cancelled with its event.
/// </summary>
/// <param name="stores">Stores.</param>
/// <param name="spelling">Spell checker.</param>
/// <param name="modelLayer">Model layer.</param>
/// <param name="corpus">Library lyrics corpus.</param>
/// <param name="events">Event log.</param>
/// <param name="options">Options.</param>
/// <param name="services">Optional services (library gate).</param>
/// <param name="clock">Clock.</param>
public sealed class LyricAssistExecutor(
    LyricAssistStores stores, ISpellChecker spelling, LyricModelLayer modelLayer, LyricsCorpusProvider corpus, AssistEventLog events, LyricAssistHostOptions options, IServiceProvider services, TimeProvider clock)
{
    private LibraryGate? Gate => services.GetService<LibraryGate>();

    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Runs the assist until it ends.</summary>
    /// <param name="assistId">Assist id.</param>
    /// <param name="cancellationToken">Cancels the run (user cancel, sheet deleted, shutdown).</param>
    /// <returns>A task.</returns>
    public async Task ExecuteAsync(Guid assistId, CancellationToken cancellationToken)
    {
        var assist = await stores.Assists.FindAsync(assistId, cancellationToken);
        if (assist is null || assist.Status.IsTerminal())
        {
            return;
        }

        try
        {
            await (Gate?.WaitWhileMaintenanceAsync(cancellationToken) ?? Task.CompletedTask);
            await RunAsync(assist, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelledAsync(assist);
        }
        catch (LyricAssistException failure)
        {
            await FailAsync(assist, LyricAssistMapper.Map<AssistFailureCode>(failure.Code), failure.Message, failure.Fix);
        }
    }

    /// <summary>Failure message when Claude was selected after the assist was created and its lyric notice is not acknowledged.</summary>
    public const string ClaudeNoticeMessage = "Claude was selected after the assist started; nothing was sent. Acknowledge the lyric-assistant notice first.";

    /// <summary>Fix of <see cref="ClaudeNoticeMessage"/>.</summary>
    public const string ClaudeNoticeFix = "Open the assistant, read the Claude notice and confirm it, then run the assist again.";

    /// <summary>Failure message when a save before the run made the sheet match library lyrics.</summary>
    public const string NotOwnMessage = "The saved sheet now matches lyrics in your library; the assistant only works on your own lyrics.";

    /// <summary>Ends an assist as cancelled with its event.</summary>
    /// <param name="assist">The tracked assist.</param>
    /// <returns>A task.</returns>
    public async Task CancelledAsync(AssistRun assist)
    {
        ArgumentNullException.ThrowIfNull(assist);
        assist.Cancel(Now);
        await events.AppendAsync(assist.Id, LyricAssistEventTypes.Cancelled, new LyricAssistCancelledEventDto { AssistId = assist.Id }, CancellationToken.None);
    }

    /// <summary>Ends an assist as failed with its event.</summary>
    /// <param name="assist">The tracked assist.</param>
    /// <param name="code">Code.</param>
    /// <param name="message">Message.</param>
    /// <param name="fix">Fix.</param>
    /// <returns>A task.</returns>
    public async Task FailAsync(AssistRun assist, AssistFailureCode code, string message, string? fix)
    {
        ArgumentNullException.ThrowIfNull(assist);
        assist.Fail(code, message, fix, Now);
        await events.AppendAsync(assist.Id, LyricAssistEventTypes.Failed,
            new LyricAssistFailedEventDto { Code = LyricAssistMapper.Wire(LyricAssistMapper.Map<AssistFailureCodeDto>(code)), Message = assist.FailureMessage!, Fix = assist.FailureFix },
            CancellationToken.None);
    }

    private async Task RunAsync(AssistRun assist, CancellationToken cancellationToken)
    {
        var sheet = await stores.Sheets.FindAsync(assist.SheetId, cancellationToken) ?? throw new OperationCanceledException(cancellationToken);
        var model = await modelLayer.ResolveAsync(sheet.Title, assist.Kind, cancellationToken);
        await RunTimeChecksAsync(assist, sheet, model, cancellationToken);
        var blocks = model.Available ? SheetBlocks.Split(sheet.Text, options.BlockLines, assist.LineFrom, assist.LineTo) : [];
        assist.Start([.. LyricModelLayer.Deterministic(assist.Language), model.Layer()], blocks.Count, Now);
        await events.AppendAsync(assist.Id, LyricAssistEventTypes.Started, Started(assist, model), cancellationToken);
        var misses = assist.Kind == AssistKind.Check ? await DictionaryAsync(assist, sheet, cancellationToken) : [];
        var sink = new AssistSink(await RunContextAsync(assist, sheet, model.Available ? misses : [], cancellationToken)) { Blocks = blocks };
        if (!model.Available)
        {
            await sink.OfferUnknownAsync(misses, cancellationToken);
        }

        if (model.Assistant is { } assistant && blocks.Count > 0)
        {
            await assistant.RunAsync(LyricAssistJobs.Build(assist, sheet, blocks, misses), sink, cancellationToken);
        }

        assist.Complete(Now);
        await events.AppendAsync(assist.Id, LyricAssistEventTypes.Completed, Completed(assist), CancellationToken.None);
    }

    // The create-time guards again at run time (security review): the backend may have switched to Claude, or the sheet may have been
    // saved, between the create request and the start of the run (queue, library maintenance).
    private async Task RunTimeChecksAsync(AssistRun assist, LyricsSheet sheet, ModelLayerState model, CancellationToken cancellationToken)
    {
        if (model.Available && model.Backend == AgentBackendDto.Claude && !(await stores.Words.SettingsAsync(cancellationToken)).HasAcknowledged(LyricAssistLimits.ClaudeNoticeVersion))
        {
            throw new LyricAssistException(AssistFailureCodeDto.BackendUnavailable, ClaudeNoticeMessage, ClaudeNoticeFix);
        }

        if (sheet.Revision != assist.SheetRevision && VerbatimGuard.RefusingTrack(sheet.Text, await corpus.RefreshAsync(cancellationToken), sheet.OwnWorkOverride?.MatchedTrackId) is not null)
        {
            throw new LyricAssistException(AssistFailureCodeDto.Refusal, NotOwnMessage);
        }
    }

    private async Task<IReadOnlyList<WordMiss>> DictionaryAsync(AssistRun assist, LyricsSheet sheet, CancellationToken cancellationToken)
    {
        var words = (await stores.Words.ListAsync(assist.Language, cancellationToken)).Select(word => word.Word).ToHashSet(StringComparer.Ordinal);
        var result = new DictionaryCheck(spelling, words).Run(sheet.Text, assist.Language, (assist.LineFrom, assist.LineTo), cancellationToken);
        var sink = new AssistSink(await RunContextAsync(assist, sheet, [], cancellationToken));
        foreach (var note in result.Notes)
        {
            await sink.OfferAsync(note, cancellationToken);
        }

        return result.Misses;
    }

    private async Task<AssistRunContext> RunContextAsync(AssistRun assist, LyricsSheet sheet, IReadOnlyList<WordMiss> misses, CancellationToken cancellationToken)
    {
        var words = (await stores.Words.ListAsync(assist.Language, cancellationToken)).Select(word => word.Word).ToList();
        return new AssistRunContext
        {
            Assist = assist,
            Filter = new FilterContext(assist.Kind, assist.Sensitivity, sheet.Text, sheet.IntentionalMarks, words, assist.Language),
            Misses = misses,
            Suggestions = stores.Suggestions,
            Events = events,
            Gate = Gate,
            Clock = clock,
            Corpus = await corpus.RefreshAsync(cancellationToken),
            CurrentText = ct => stores.Sheets.CurrentTextAsync(sheet.Id, ct),
        };
    }

    private static LyricAssistStartedEventDto Started(AssistRun assist, ModelLayerState model) => new()
    {
        AssistId = assist.Id,
        Layers = assist.Layers.Select(LyricAssistMapper.ToDto).ToList(),
        Backend = model.Available ? LyricAssistMapper.Wire(model.Backend) : null,
        UsingFallback = assist.UsingFallback,
        BlocksTotal = assist.BlocksTotal,
        EstimatedSeconds = assist.EstimatedSeconds,
    };

    private LyricAssistCompletedEventDto Completed(AssistRun assist) => new()
    {
        Counts = LyricAssistMapper.ToDto(assist.Counts),
        Discarded = LyricAssistMapper.ToDto(assist.Discarded),
        DurationMs = assist.DurationMs(Now),
    };
}
