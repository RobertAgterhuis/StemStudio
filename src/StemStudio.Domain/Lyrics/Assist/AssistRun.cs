namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>One layer of an assist as shown in the pane.</summary>
/// <param name="Layer">Layer.</param>
/// <param name="Name">Display name (at most 100).</param>
/// <param name="Available">True when it ran.</param>
/// <param name="Reason">Why it did not run.</param>
/// <param name="Fix">How to make it run.</param>
public sealed record AssistLayer(SuggestionSource Layer, string Name, bool Available, string? Reason = null, string? Fix = null);

/// <summary>Shown suggestions per category.</summary>
public sealed record CategoryCounts
{
    /// <summary>Spelling.</summary>
    public int Spelling { get; init; }

    /// <summary>Grammar.</summary>
    public int Grammar { get; init; }

    /// <summary>Flow.</summary>
    public int Flow { get; init; }

    /// <summary>Licence.</summary>
    public int Licence { get; init; }

    /// <summary>Device.</summary>
    public int Device { get; init; }

    /// <summary>Alternative.</summary>
    public int Alternative { get; init; }

    /// <summary>Continuation.</summary>
    public int Continuation { get; init; }

    /// <summary>Title.</summary>
    public int Title { get; init; }

    /// <summary>The counts with one more suggestion of a category.</summary>
    /// <param name="category">Category.</param>
    /// <returns>New counts.</returns>
    public CategoryCounts Add(SuggestionCategory category) => category switch
    {
        SuggestionCategory.Spelling => this with { Spelling = Spelling + 1 },
        SuggestionCategory.Grammar => this with { Grammar = Grammar + 1 },
        SuggestionCategory.Flow => this with { Flow = Flow + 1 },
        SuggestionCategory.Licence => this with { Licence = Licence + 1 },
        SuggestionCategory.Device => this with { Device = Device + 1 },
        SuggestionCategory.Alternative => this with { Alternative = Alternative + 1 },
        SuggestionCategory.Continuation => this with { Continuation = Continuation + 1 },
        _ => this with { Title = Title + 1 },
    };
}

/// <summary>Dropped items per reason (Expert log).</summary>
public sealed record DiscardedCounts
{
    /// <summary>Anchor did not match.</summary>
    public int InvalidAnchor { get; init; }

    /// <summary>Output did not validate.</summary>
    public int InvalidSchema { get; init; }

    /// <summary>A rhyme or syllable claim failed.</summary>
    public int FailedVerification { get; init; }

    /// <summary>Removed by sensitivity, marks or personal words.</summary>
    public int Filtered { get; init; }

    /// <summary>Withheld as too close to library lyrics.</summary>
    public int Verbatim { get; init; }

    /// <summary>The sum of two counts.</summary>
    /// <param name="other">Other counts.</param>
    /// <returns>The sum.</returns>
    public DiscardedCounts Plus(DiscardedCounts other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new DiscardedCounts
        {
            InvalidAnchor = InvalidAnchor + other.InvalidAnchor, InvalidSchema = InvalidSchema + other.InvalidSchema,
            FailedVerification = FailedVerification + other.FailedVerification, Filtered = Filtered + other.Filtered, Verbatim = Verbatim + other.Verbatim,
        };
    }
}

/// <summary>Claude token usage of an assist.</summary>
/// <param name="InputTokens">Input tokens.</param>
/// <param name="OutputTokens">Output tokens.</param>
/// <param name="CachedInputTokens">Cached input tokens.</param>
public sealed record AssistUsage(int InputTokens, int OutputTokens, int CachedInputTokens);

/// <summary>The request of an assist (parameter object of <see cref="AssistRun.Create"/>).</summary>
public sealed record AssistRequest
{
    /// <summary>Kind.</summary>
    public required AssistKind Kind { get; init; }

    /// <summary>Categories (already defaulted).</summary>
    public IReadOnlyList<SuggestionCategory> Categories { get; init; } = [];

    /// <summary>Range start.</summary>
    public int? LineFrom { get; init; }

    /// <summary>Range end.</summary>
    public int? LineTo { get; init; }

    /// <summary>Count of the writing kinds.</summary>
    public int? Count { get; init; }

    /// <summary>Instruction.</summary>
    public string? Instruction { get; init; }

    /// <summary>Origin.</summary>
    public AssistOrigin Origin { get; init; } = AssistOrigin.Editor;

    /// <summary>Agent run that started it.</summary>
    public Guid? AgentRunId { get; init; }
}

/// <summary>One assistant action on a sheet revision (data-model 3).</summary>
public sealed class AssistRun
{
    private AssistRun()
    {
        Categories = [];
        Layers = [];
        Counts = new CategoryCounts();
        Discarded = new DiscardedCounts();
    }

    /// <summary>Identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>Sheet.</summary>
    public Guid SheetId { get; private set; }

    /// <summary>The revision checked.</summary>
    public int SheetRevision { get; private set; }

    /// <summary>Kind.</summary>
    public AssistKind Kind { get; private set; }

    /// <summary>Requested categories.</summary>
    public IReadOnlyList<SuggestionCategory> Categories { get; private set; }

    /// <summary>Range start.</summary>
    public int? LineFrom { get; private set; }

    /// <summary>Range end.</summary>
    public int? LineTo { get; private set; }

    /// <summary>Count.</summary>
    public int? Count { get; private set; }

    /// <summary>Instruction.</summary>
    public string? Instruction { get; private set; }

    /// <summary>Sensitivity at the start.</summary>
    public Sensitivity Sensitivity { get; private set; }

    /// <summary>Language.</summary>
    public AssistLanguage Language { get; private set; }

    /// <summary>Status.</summary>
    public AssistStatus Status { get; private set; }

    /// <summary>Layers.</summary>
    public IReadOnlyList<AssistLayer> Layers { get; private set; }

    /// <summary>Model backend (<c>ollama</c>, <c>claude</c>, <c>fake</c>) or null.</summary>
    public string? Backend { get; private set; }

    /// <summary>Model name or null.</summary>
    public string? Model { get; private set; }

    /// <summary>True while the CPU fallback answers.</summary>
    public bool UsingFallback { get; private set; }

    /// <summary>Blocks of the model layer.</summary>
    public int BlocksTotal { get; private set; }

    /// <summary>Blocks done.</summary>
    public int BlocksDone { get; private set; }

    /// <summary>Blocks skipped.</summary>
    public int BlocksSkipped { get; private set; }

    /// <summary>Shown suggestions per category.</summary>
    public CategoryCounts Counts { get; private set; }

    /// <summary>Dropped items.</summary>
    public DiscardedCounts Discarded { get; private set; }

    /// <summary>Claude usage.</summary>
    public AssistUsage? Usage { get; private set; }

    /// <summary>Origin.</summary>
    public AssistOrigin Origin { get; private set; }

    /// <summary>Agent run.</summary>
    public Guid? AgentRunId { get; private set; }

    /// <summary>Failure code.</summary>
    public AssistFailureCode? FailureCode { get; private set; }

    /// <summary>Failure message.</summary>
    public string? FailureMessage { get; private set; }

    /// <summary>Failure fix.</summary>
    public string? FailureFix { get; private set; }

    /// <summary>Estimated seconds of the model layer.</summary>
    public double? EstimatedSeconds { get; private set; }

    /// <summary>Creation time (UTC).</summary>
    public DateTime CreatedAt { get; private set; }

    /// <summary>Start time.</summary>
    public DateTime? StartedAt { get; private set; }

    /// <summary>End time.</summary>
    public DateTime? EndedAt { get; private set; }

    /// <summary>Creates a queued assist for the sheet's current revision.</summary>
    /// <param name="id">Identifier.</param>
    /// <param name="sheet">The sheet (language nl or en).</param>
    /// <param name="request">The request.</param>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>The assist.</returns>
    public static AssistRun Create(Guid id, LyricsSheet sheet, AssistRequest request, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        ArgumentNullException.ThrowIfNull(request);
        return new AssistRun
        {
            Id = id, SheetId = sheet.Id, SheetRevision = sheet.Revision, Kind = request.Kind, Categories = request.Categories,
            LineFrom = request.LineFrom, LineTo = request.LineTo, Count = request.Count, Instruction = request.Instruction,
            Sensitivity = sheet.Sensitivity, Language = AssistEnumExtensions.AssistLanguageOf(sheet.Language) ?? AssistLanguage.English,
            Status = AssistStatus.Queued, Origin = request.Origin, AgentRunId = request.AgentRunId, CreatedAt = nowUtc,
        };
    }

    /// <summary>Marks the assist running with its layers.</summary>
    /// <param name="layers">Layers.</param>
    /// <param name="blocksTotal">Model blocks (0 when the model layer does not run).</param>
    /// <param name="nowUtc">Current time.</param>
    public void Start(IReadOnlyList<AssistLayer> layers, int blocksTotal, DateTime nowUtc) =>
        (Status, Layers, BlocksTotal, StartedAt) = (AssistStatus.Running, layers, blocksTotal, nowUtc);

    /// <summary>Records the model that answers.</summary>
    /// <param name="backend">Backend.</param>
    /// <param name="model">Model.</param>
    /// <param name="usingFallback">Fallback in use.</param>
    /// <param name="estimatedSeconds">Estimate.</param>
    public void UseModel(string backend, string model, bool usingFallback, double? estimatedSeconds) =>
        (Backend, Model, UsingFallback, EstimatedSeconds) = (backend, model, usingFallback, estimatedSeconds);

    /// <summary>Counts a finished block.</summary>
    /// <param name="skipped">True when skipped.</param>
    /// <param name="discarded">Items the block dropped.</param>
    public void BlockDone(bool skipped, DiscardedCounts discarded)
    {
        BlocksDone++;
        BlocksSkipped += skipped ? 1 : 0;
        Discarded = Discarded.Plus(discarded);
    }

    /// <summary>Counts a shown suggestion.</summary>
    /// <param name="category">Category.</param>
    public void CountShown(SuggestionCategory category) => Counts = Counts.Add(category);

    /// <summary>Counts dropped items.</summary>
    /// <param name="discarded">Items.</param>
    public void CountDiscarded(DiscardedCounts discarded) => Discarded = Discarded.Plus(discarded);

    /// <summary>Records the Claude usage.</summary>
    /// <param name="usage">Usage so far.</param>
    public void RecordUsage(AssistUsage usage) => Usage = usage;

    /// <summary>Ends the assist successfully.</summary>
    /// <param name="nowUtc">Current time.</param>
    public void Complete(DateTime nowUtc) => End(AssistStatus.Completed, nowUtc);

    /// <summary>Cancels the assist.</summary>
    /// <param name="nowUtc">Current time.</param>
    public void Cancel(DateTime nowUtc) => End(AssistStatus.Cancelled, nowUtc);

    /// <summary>Fails the assist.</summary>
    /// <param name="code">Code.</param>
    /// <param name="message">Message (at most 500).</param>
    /// <param name="fix">Fix.</param>
    /// <param name="nowUtc">Current time.</param>
    public void Fail(AssistFailureCode code, string message, string? fix, DateTime nowUtc)
    {
        (FailureCode, FailureMessage, FailureFix) = (code, Domain.Text.TrimToLength(message, 500) ?? code.ToString(), Domain.Text.TrimToLength(fix, 500));
        End(AssistStatus.Failed, nowUtc);
    }

    /// <summary>Duration in milliseconds (0 before the start).</summary>
    /// <param name="nowUtc">Current time.</param>
    /// <returns>Milliseconds.</returns>
    public long DurationMs(DateTime nowUtc) => StartedAt is { } start ? (long)((EndedAt ?? nowUtc) - start).TotalMilliseconds : 0;

    private void End(AssistStatus status, DateTime nowUtc) => (Status, EndedAt) = (status, nowUtc);
}
