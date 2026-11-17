using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// The newest assist of the open sheet as the pane shows it (data-model 3 and 10): status, progress, layers and model, counts,
/// withheld items, notes, usage and the failure. Filled from the create response, the events and the snapshot.
/// </summary>
public sealed class AssistRun
{
    private static readonly Dictionary<string, Action<AssistRun, LyricAssistEvent>> Handlers = new(StringComparer.Ordinal)
    {
        [LyricAssistEventTypes.Started] = (run, e) => run.Started(e.Read<LyricAssistStartedEventDto>()),
        [LyricAssistEventTypes.Progress] = (run, e) => run.Progress(e.Read<LyricAssistProgressEventDto>()),
        [LyricAssistEventTypes.Note] = (run, e) => run._notes.Add(e.Read<LyricAssistNoteEventDto>()),
        [LyricAssistEventTypes.UsageUpdated] = (run, e) => run.Usage = e.Read<TokenUsageDto>(),
        [LyricAssistEventTypes.Completed] = (run, e) => run.Completed(e.Read<LyricAssistCompletedEventDto>()),
        [LyricAssistEventTypes.Failed] = (run, e) => run.Failed(e.Read<LyricAssistFailedEventDto>()),
        [LyricAssistEventTypes.Cancelled] = (run, _) => run.Status = AssistStatusDto.Cancelled,
    };

    private readonly List<LyricAssistNoteEventDto> _notes = [];

    /// <summary>The assist.</summary>
    public required Guid AssistId { get; init; }

    /// <summary>The kind.</summary>
    public required AssistKindDto Kind { get; init; }

    /// <summary>The status.</summary>
    public AssistStatusDto Status { get; private set; } = AssistStatusDto.Queued;

    /// <summary>The layers with their availability.</summary>
    public IReadOnlyList<LayerInfoDto> Layers { get; private set; } = [];

    /// <summary>The model backend (<c>ollama</c>, <c>claude</c>, ...), or null without a model.</summary>
    public string? Backend { get; private set; }

    /// <summary>The model, or null.</summary>
    public string? Model { get; private set; }

    /// <summary>True when the CPU fallback model runs (a heavy GPU job is running).</summary>
    public bool UsingFallback { get; private set; }

    /// <summary>Expected duration in seconds, or null.</summary>
    public double? EstimatedSeconds { get; private set; }

    /// <summary>Blocks processed.</summary>
    public int BlocksDone { get; private set; }

    /// <summary>Blocks of the assist.</summary>
    public int BlocksTotal { get; private set; }

    /// <summary>Counts per category (at completion).</summary>
    public CategoryCountsDto Counts { get; private set; } = CategoryCountsDto.None;

    /// <summary>Discarded items (Expert log; <c>verbatim</c> is the withheld counter).</summary>
    public DiscardedCountsDto Discarded { get; private set; } = DiscardedCountsDto.None;

    /// <summary>Token usage (Claude), or null.</summary>
    public TokenUsageDto? Usage { get; private set; }

    /// <summary>Duration of a completed assist in milliseconds, or null.</summary>
    public long? DurationMs { get; private set; }

    /// <summary>The failure message and fix, or null.</summary>
    public AssistFailureDto? Failure { get; private set; }

    /// <summary>The notes (block skipped, fallback model, quality note, verbatim withheld).</summary>
    public IReadOnlyList<LyricAssistNoteEventDto> Notes => _notes;

    /// <summary>True while queued or running.</summary>
    public bool IsActive => Status is AssistStatusDto.Queued or AssistStatusDto.Running;

    /// <summary>Progress 0-100 (blocks done / total).</summary>
    public int Percent => BlocksTotal <= 0 ? (IsActive ? 0 : 100) : Math.Clamp(BlocksDone * 100 / BlocksTotal, 0, 100);

    /// <summary>Withheld suggestions (too close to library lyrics).</summary>
    public int Withheld => Math.Max(Discarded.Verbatim, _notes.Count(n => n.Code == AssistNoteCodeDto.VerbatimWithheld));

    /// <summary>A run from a snapshot or a create response.</summary>
    /// <param name="summary">The summary.</param>
    /// <returns>The run.</returns>
    public static AssistRun From(LyricAssistSummaryDto summary)
    {
        var run = new AssistRun { AssistId = summary.AssistId, Kind = summary.Kind };
        run.Take(summary);
        return run;
    }

    /// <summary>Takes the values of a snapshot.</summary>
    /// <param name="summary">The summary.</param>
    public void Take(LyricAssistSummaryDto summary)
    {
        (Status, Layers, Model, UsingFallback, EstimatedSeconds) = (summary.Status, summary.Layers, summary.Model, summary.UsingFallback, summary.EstimatedSeconds);
        (BlocksDone, BlocksTotal, Counts, Discarded, Usage, Failure) = (summary.BlocksDone, summary.BlocksTotal, summary.Counts, summary.Discarded, summary.Usage, summary.Failure);
        Backend = summary.Backend is { } backend ? StemStudio.Web.Agent.AgentWire.Name(backend) : null;
        DurationMs = summary is { StartedAt: { } start, EndedAt: { } end } ? (long)(end - start).TotalMilliseconds : DurationMs;
    }

    /// <summary>Applies one event; suggestion events are handled by the board.</summary>
    /// <param name="e">The event.</param>
    public void Apply(LyricAssistEvent e)
    {
        if (Handlers.TryGetValue(e.Type, out var handle))
        {
            handle(this, e);
        }
    }

    private void Started(LyricAssistStartedEventDto e) =>
        (Status, Layers, Backend, Model, UsingFallback, BlocksTotal, EstimatedSeconds) =
        (AssistStatusDto.Running, e.Layers, e.Backend, e.Model, e.UsingFallback, e.BlocksTotal, e.EstimatedSeconds);

    private void Progress(LyricAssistProgressEventDto e) => (BlocksDone, BlocksTotal) = (e.BlocksDone + e.BlocksSkipped, e.BlocksTotal);

    private void Completed(LyricAssistCompletedEventDto e) =>
        (Status, Counts, Discarded, DurationMs, BlocksDone) = (AssistStatusDto.Completed, e.Counts, e.Discarded, e.DurationMs, BlocksTotal);

    private void Failed(LyricAssistFailedEventDto e)
    {
        Status = AssistStatusDto.Failed;
        Failure = new AssistFailureDto { Code = AssistFailureCodeDto.Internal, Message = e.Message, Fix = e.Fix };
    }
}
