using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>Limits of the assistant (data-model 1: <c>StemStudio__LyricAssist__*</c>).</summary>
public sealed record LyricAssistOptions
{
    /// <summary>Claude input token cap per assist (FR-026).</summary>
    public int MaxInputTokensPerAssist { get; init; } = LyricAssistLimits.DefaultMaxInputTokensPerAssist;

    /// <summary>Claude output token cap per assist (FR-026).</summary>
    public int MaxOutputTokensPerAssist { get; init; } = LyricAssistLimits.DefaultMaxOutputTokensPerAssist;

    /// <summary>Timeout of one block with a local model.</summary>
    public TimeSpan LocalBlockTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Timeout of one block with Claude.</summary>
    public TimeSpan ClaudeBlockTimeout { get; init; } = TimeSpan.FromSeconds(90);
}

/// <summary>
/// The tool-less lyric assistant (ADR-037 D1, FR-021): blocks run one after the other; each block is one schema-constrained model
/// call with the whole sheet as delimited data, retried once with a stricter instruction on invalid output and otherwise skipped
/// with a note; a block that times out is skipped too. Every item is validated (<see cref="SuggestionValidator"/>) before it reaches
/// the sink, so nothing the model writes is trusted. Claude usage is summed per assist and the assist stops with
/// <c>budget-exhausted</c> when a cap is reached. Cancellation is checked between blocks and passed into the model call.
/// </summary>
/// <param name="model">The model layer (also its selection).</param>
/// <param name="verification">Syllables, rhymes and the verbatim guard (backend adapter over the Domain services).</param>
/// <param name="options">Limits; null = the defaults.</param>
/// <param name="time">Clock of the block timeouts; null = the system clock.</param>
public sealed class LyricAssistant(ILyricModel model, ILyricVerification verification, LyricAssistOptions? options = null, TimeProvider? time = null) : ILyricAssistant
{
    /// <summary>Note when the CPU fallback model runs.</summary>
    public const string FallbackNote = "A GPU job is running: the assistant uses the smaller model on the CPU.";

    /// <summary>Note of a block skipped after invalid output twice.</summary>
    public const string InvalidNote = "Lines {0}-{1} were skipped: the model did not answer in the expected format.";

    /// <summary>Note of a block that timed out.</summary>
    public const string TimeoutNote = "Lines {0}-{1} were skipped: the model took too long.";

    /// <summary>Note when generated text was withheld as library lyrics.</summary>
    public const string VerbatimNote = "Some generated text was withheld because it matched song lyrics in your library.";

    private readonly LyricAssistOptions _options = options ?? new LyricAssistOptions();
    private readonly TimeProvider _time = time ?? TimeProvider.System;

    /// <inheritdoc />
    public async Task RunAsync(LyricAssistJob job, ILyricAssistSink sink, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(sink);
        var info = model is ILyricModelSelection selection ? await selection.SelectAsync(cancellationToken) : ScriptedInfo;
        await sink.StartedAsync(info, cancellationToken);
        if (info.UsingFallback)
        {
            await sink.NoteAsync(AssistNoteCodeDto.FallbackModel, FallbackNote, cancellationToken);
        }

        var run = new AssistRun(job, info, sink);
        foreach (var block in job.Blocks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CheckBudget(run);
            await CompleteBlockAsync(run, block, cancellationToken);
        }
    }

    private static LyricModelInfo ScriptedInfo => Fakes.ScriptedLyricModel.Info;

    private async Task CompleteBlockAsync(AssistRun run, LyricBlock block, CancellationToken cancellationToken)
    {
        var (result, note) = await BlockAsync(run, block, cancellationToken);
        await run.Sink.BlockCompletedAsync(result, cancellationToken);
        if (note is not null)
        {
            await run.Sink.NoteAsync(AssistNoteCodeDto.BlockSkipped, string.Format(System.Globalization.CultureInfo.InvariantCulture, note, block.FirstLine, block.LastLine), cancellationToken);
        }

        if (result.Discarded.Verbatim > 0)
        {
            await run.Sink.NoteAsync(AssistNoteCodeDto.VerbatimWithheld, VerbatimNote, cancellationToken);
        }

        if (run.Usage is { } usage)
        {
            await run.Sink.UsageAsync(usage, cancellationToken);
        }
    }

    /// <summary>One block: up to two calls (the second with the stricter instruction); a timeout or invalid output twice skips it.</summary>
    private async Task<(LyricBlockResult Result, string? Note)> BlockAsync(AssistRun run, LyricBlock block, CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var reply = await CallAsync(run, block, attempt == 1, cancellationToken);
            if (reply is null)
            {
                return (Skipped(block), TimeoutNote);
            }

            run.Add(reply.Usage);
            if (LyricOutputParser.Parse(reply.Json) is { } output)
            {
                return (SuggestionValidator.Validate(new ValidationContext(run.Job, block, verification, Guid.NewGuid()), output), null);
            }
        }

        return (Skipped(block), InvalidNote);
    }

    private async Task<LyricModelReply?> CallAsync(AssistRun run, LyricBlock block, bool strict, CancellationToken cancellationToken)
    {
        var prompt = LyricPromptBuilder.Build(run.Job, block, LyricPromptBuilder.NewBoundary(), strict);
        var request = new LyricModelRequest
        {
            Kind = run.Job.Kind,
            BlockIndex = block.Index,
            SystemPrompt = prompt.System,
            UserPrompt = prompt.User,
            Temperature = LyricPromptBuilder.Temperature(run.Job.Kind),
            MaxOutputTokens = run.IsClaude ? Math.Max(1, _options.MaxOutputTokensPerAssist - (int)(run.Usage?.OutputTokens ?? 0)) : null,
        };
        using var timeout = new CancellationTokenSource(run.IsClaude ? _options.ClaudeBlockTimeout : _options.LocalBlockTimeout, _time);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        try
        {
            return await model.CompleteAsync(request, linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private void CheckBudget(AssistRun run)
    {
        if (run.Usage is { } usage && (usage.InputTokens >= _options.MaxInputTokensPerAssist || usage.OutputTokens >= _options.MaxOutputTokensPerAssist))
        {
            throw new LyricAssistException(
                AssistFailureCodeDto.BudgetExhausted,
                $"The Claude limit of this assist is reached ({usage.InputTokens} input, {usage.OutputTokens} output tokens).",
                "Check fewer lines at a time, or switch to the local model.");
        }
    }

    private static LyricBlockResult Skipped(LyricBlock block) => new() { BlockIndex = block.Index, Skipped = true };

    /// <summary>State of one assist run (job, model, sink and the summed Claude usage).</summary>
    private sealed class AssistRun(LyricAssistJob job, LyricModelInfo info, ILyricAssistSink sink)
    {
        public LyricAssistJob Job { get; } = job;

        public ILyricAssistSink Sink { get; } = sink;

        public bool IsClaude { get; } = info.Backend == AgentBackendDto.Claude;

        public TokenUsageDto? Usage { get; private set; }

        public void Add(TokenUsageDto? usage)
        {
            if (usage is not null)
            {
                Usage = new TokenUsageDto
                {
                    InputTokens = (Usage?.InputTokens ?? 0) + usage.InputTokens,
                    OutputTokens = (Usage?.OutputTokens ?? 0) + usage.OutputTokens,
                    CachedInputTokens = (Usage?.CachedInputTokens ?? 0) + usage.CachedInputTokens,
                };
            }
        }
    }
}
