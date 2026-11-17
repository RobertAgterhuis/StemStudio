using StemStudio.Agent.LyricAssist;
using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>The stores and services an assist run writes to (parameter object of <see cref="AssistSink"/>).</summary>
public sealed record AssistRunContext
{
    /// <summary>The assist (tracked).</summary>
    public required AssistRun Assist { get; init; }

    /// <summary>Filter context (marks, personal words, sensitivity, the checked text).</summary>
    public required FilterContext Filter { get; init; }

    /// <summary>The dictionary misses of the assist.</summary>
    public required IReadOnlyList<WordMiss> Misses { get; init; }

    /// <summary>Suggestion store.</summary>
    public required ILyricSuggestionRepository Suggestions { get; init; }

    /// <summary>Event log.</summary>
    public required AssistEventLog Events { get; init; }

    /// <summary>Library lyrics for the output guard (defence in depth after the assistant's own check).</summary>
    public required ILyricsCorpus Corpus { get; init; }

    /// <summary>Library gate (013).</summary>
    public LibraryGate? Gate { get; init; }

    /// <summary>Clock.</summary>
    public required TimeProvider Clock { get; init; }
}

/// <summary>
/// The backend's <see cref="ILyricAssistSink"/> (DELTA.md 2): applies the Domain <see cref="SuggestionFilter"/> to dictionary and model
/// suggestions alike (counting <c>filtered</c>), persists suggestions and events, classifies dictionary misses with the model's verdicts
/// and waits on the library gate between blocks.
/// </summary>
/// <param name="run">The run context.</param>
public sealed class AssistSink(AssistRunContext run) : ILyricAssistSink
{
    private AssistRun Assist => run.Assist;

    private DateTime Now => run.Clock.GetUtcNow().UtcDateTime;

    /// <summary>Offers a suggestion: filtered or stored and streamed.</summary>
    /// <param name="draft">The suggestion.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task OfferAsync(SuggestionDraft draft, CancellationToken cancellationToken)
    {
        if (draft.Source == SuggestionSource.Model && (VerbatimGuard.IsVerbatim(draft.Proposal, run.Corpus) || VerbatimGuard.IsVerbatim(draft.Explanation, run.Corpus)))
        {
            Assist.CountDiscarded(new DiscardedCounts { Verbatim = 1 });
            return;
        }

        if (!SuggestionFilter.Keep(draft, run.Filter) || Total() >= LyricAssistLimits.MaxSuggestions)
        {
            Assist.CountDiscarded(new DiscardedCounts { Filtered = 1 });
            return;
        }

        var suggestion = LyricSuggestion.Create(Guid.NewGuid(), Assist, draft, Now);
        run.Suggestions.AddRange([suggestion]);
        Assist.CountShown(suggestion.Category);
        await run.Events.AppendAsync(Assist.Id, LyricAssistEventTypes.SuggestionAdded, LyricAssistMapper.ToDto(suggestion), cancellationToken);
    }

    /// <summary>Offers dictionary misses as improvements when no model classifies them (research R-03 step 5).</summary>
    /// <param name="misses">The misses.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public async Task OfferUnknownAsync(IEnumerable<WordMiss> misses, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(misses);
        foreach (var miss in misses)
        {
            await OfferAsync(DictionaryCheck.Unknown(miss, run.Filter.Language), cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task StartedAsync(LyricModelInfo model, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(model);
        Assist.UseModel(Wire(model.Backend), model.Model, model.UsingFallback, Assist.EstimatedSeconds);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task BlockCompletedAsync(LyricBlockResult result, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        await (run.Gate?.WaitWhileMaintenanceAsync(cancellationToken) ?? Task.CompletedTask);
        foreach (var suggestion in result.Suggestions)
        {
            await OfferAsync(Draft(suggestion), cancellationToken);
        }

        foreach (var draft in MissDrafts(result))
        {
            await OfferAsync(draft, cancellationToken);
        }

        Assist.BlockDone(result.Skipped, LyricAssistMapper.ToDomain(result.Discarded));
        await run.Events.AppendAsync(Assist.Id, LyricAssistEventTypes.Progress,
            new LyricAssistProgressEventDto { BlocksDone = Assist.BlocksDone, BlocksSkipped = Assist.BlocksSkipped, BlocksTotal = Assist.BlocksTotal }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task NoteAsync(AssistNoteCodeDto code, string message, CancellationToken cancellationToken) =>
        await run.Events.AppendAsync(Assist.Id, LyricAssistEventTypes.Note, new LyricAssistNoteEventDto { Code = code, Message = Cut(message, 300) }, cancellationToken);

    /// <inheritdoc />
    public async Task UsageAsync(TokenUsageDto usage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(usage);
        Assist.RecordUsage(new AssistUsage(usage.InputTokens, usage.OutputTokens, usage.CachedInputTokens));
        await run.Events.AppendAsync(Assist.Id, LyricAssistEventTypes.UsageUpdated, usage, cancellationToken);
    }

    private IEnumerable<SuggestionDraft> MissDrafts(LyricBlockResult result)
    {
        var language = run.Filter.Language;
        var model = Assist.Model ?? "model";
        foreach (var miss in BlockMisses(result.BlockIndex))
        {
            var verdict = result.Misses.FirstOrDefault(v => v.Line == miss.Line && v.Word == miss.Word);
            var draft = verdict?.Verdict switch
            {
                MissVerdict.Keep => null,
                MissVerdict.Typo when !string.IsNullOrWhiteSpace(verdict.Correction) => DictionaryCheck.Typo(miss, Cut(verdict.Correction, 40), language, model),
                MissVerdict.Licence => (DictionaryCheck.Licence(miss, LicenceType(verdict.LicenceType), language) with { Source = SuggestionSource.Model, Model = model }),
                _ => DictionaryCheck.Unknown(miss, language),
            };
            if (draft is not null)
            {
                yield return draft;
            }
        }
    }

    private IEnumerable<WordMiss> BlockMisses(int blockIndex) =>
        Blocks is { } blocks && blockIndex >= 0 && blockIndex < blocks.Count
            ? run.Misses.Where(m => m.Line >= blocks[blockIndex].FirstLine && m.Line <= blocks[blockIndex].LastLine)
            : [];

    /// <summary>The blocks of the model layer (set by the executor).</summary>
    public IReadOnlyList<SheetBlock>? Blocks { get; init; }

    private SuggestionDraft Draft(AssistantSuggestion suggestion) => new()
    {
        Type = LyricAssistMapper.Map<SuggestionType>(suggestion.Type), Severity = LyricAssistMapper.Map<SuggestionSeverity>(suggestion.Severity),
        Anchor = LyricAssistMapper.ToDomain(suggestion.Anchor), Operation = LyricAssistMapper.Map<SuggestionOperation>(suggestion.Operation),
        Proposal = suggestion.Proposal, GroupId = suggestion.GroupId, Explanation = suggestion.Explanation, Source = SuggestionSource.Model, Model = Assist.Model,
        SyllablesBefore = suggestion.SyllablesBefore, SyllablesAfter = suggestion.SyllablesAfter,
        RhymeWith = suggestion.RhymeWith is { } rhyme ? new RhymePartner(rhyme.Line, rhyme.Word) : null,
    };

    private int Total()
    {
        var c = Assist.Counts;
        return c.Spelling + c.Grammar + c.Flow + c.Licence + c.Device + c.Alternative + c.Continuation + c.Title;
    }

    private static SuggestionType LicenceType(string? type) => type switch
    {
        "elision" => SuggestionType.LicenceElision,
        "inversion" => SuggestionType.LicenceInversion,
        "archaism" => SuggestionType.LicenceArchaism,
        "sound-spelling" => SuggestionType.LicenceSoundSpelling,
        "code-switch" => SuggestionType.LicenceCodeSwitch,
        _ => SuggestionType.LicenceSlang,
    };

    private static string Wire(AgentBackendDto backend) => backend switch
    {
        AgentBackendDto.Claude => "claude",
        AgentBackendDto.Fake => "fake",
        _ => "ollama",
    };

    private static string Cut(string? value, int max) => value is null ? string.Empty : value.Length <= max ? value : value[..max];
}
