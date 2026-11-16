using System.Diagnostics;
using System.Text.Json;
using StemStudio.Agent.Model;
using StemStudio.Agent.Runtime;
using StemStudio.Agent.Tools;
using StemStudio.Agent.Tools.Providers;
using StemStudio.AgentEval.Fixture;
using StemStudio.Contracts.Agent;

namespace StemStudio.AgentEval;

/// <summary>The outcome of one case run.</summary>
/// <param name="Case">The case.</param>
/// <param name="Grade">Its grade.</param>
/// <param name="Latency">Wall time of the run (command to end).</param>
/// <param name="Calls">The recorded calls (fixture keys instead of ids).</param>
/// <param name="Cards">Approval cards shown (with <see cref="ApprovalAnswer.None"/> each one was rejected).</param>
internal sealed record CaseRun(EvalCase Case, Grade Grade, TimeSpan Latency, IReadOnlyList<RecordedCall> Calls, int Cards);

/// <summary>How the harness talks to a model: the chat session per case (fresh fixture ids per run).</summary>
/// <param name="Backend">Run backend recorded on the run.</param>
/// <param name="Model">Model name under test.</param>
/// <param name="Session">Creates the chat session of a case run (the fake plays the case's ideal transcript).</param>
internal sealed record LiveBackend(AgentBackendDto Backend, string Model, Func<EvalCase, FixtureIds, IAgentChatSession> Session);

/// <summary>
/// Runs eval cases through the real agent runtime (<see cref="AgentRunService"/>, policy, registry, providers) over the seeded
/// fixture (<see cref="FixtureLibrary"/>), answering approval cards as the case states and grading the recorded steps.
/// </summary>
internal sealed class LiveRunner(LiveBackend backend, string presetsJson, EvalCase? undoPrelude)
{
    private static readonly TimeSpan CaseTimeout = TimeSpan.FromMinutes(15);

    /// <summary>Runs one case once.</summary>
    public async Task<CaseRun> RunAsync(EvalCase evalCase)
    {
        using var timeout = new CancellationTokenSource(CaseTimeout);
        var context = CaseContext.Create(evalCase, backend, presetsJson);
        var watch = Stopwatch.StartNew();
        var grade = await GuardedAsync(() => evalCase.Undo ? UndoAsync(context, timeout.Token) : CommandAsync(context, evalCase, timeout.Token));
        return new CaseRun(evalCase, grade, watch.Elapsed, context.Calls, context.Cards);
    }

    /// <summary>A failing case (nothing to undo, case timeout, a harness error) fails only that case, not the evaluation.</summary>
    private static async Task<Grade> GuardedAsync(Func<Task<Grade>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception exception)
        {
            return new Grade(false, $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private static async Task<Grade> CommandAsync(CaseContext context, EvalCase evalCase, CancellationToken cancellationToken)
    {
        await context.RunCommandAsync(evalCase.Command, cancellationToken);
        return context.Failure is { } failure ? new Grade(false, failure) : Grader.Grade(evalCase, context.Calls);
    }

    /// <summary>Case 19: the prelude command (case 8) changes the mixer, then the Undo request must restore it.</summary>
    private async Task<Grade> UndoAsync(CaseContext context, CancellationToken cancellationToken)
    {
        if (undoPrelude is null)
        {
            return new Grade(false, "the undo case needs case 8 as its prelude");
        }

        await context.RunCommandAsync(undoPrelude.Command, cancellationToken);
        await context.UndoAsync(cancellationToken);
        return context.Page.IsRestored ? new Grade(true, null) : new Grade(false, "the mixer was not restored");
    }
}

/// <summary>Everything of one case run: fixture, page, stores, the agent service and the approval answers.</summary>
internal sealed class CaseContext
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly EvalCase _case;
    private readonly AgentServices _services;
    private readonly AgentRunService _agent;
    private readonly Guid _conversationId = Guid.NewGuid();
    private readonly List<RecordedCall> _calls = [];
    private int _cards;

    private CaseContext(EvalCase evalCase, FixtureLibrary library, FixturePage page, LiveBackend backend)
    {
        _case = evalCase;
        Library = library;
        Page = page;
        _services = Services(backend);
        _agent = new AgentRunService(_services);
    }

    public FixtureLibrary Library { get; }

    public FixturePage Page { get; }

    public IReadOnlyList<RecordedCall> Calls => _calls;

    public int Cards => _cards;

    /// <summary>The failure of the last run (backend unreachable, timeout, limits), when it did not complete.</summary>
    public string? Failure { get; private set; }

    public static CaseContext Create(EvalCase evalCase, LiveBackend backend, string presetsJson)
    {
        var library = FixtureLibrary.Seed(presetsJson);
        var page = new FixturePage(Key(library, evalCase.TrackId), Key(library, evalCase.MixId));
        return new CaseContext(evalCase, library, page, backend);
    }

    /// <summary>Sends the command as a new run of the case's conversation and waits for its end.</summary>
    public async Task RunCommandAsync(string command, CancellationToken cancellationToken)
    {
        var runId = await EvalConversation.QueueAsync(_services, new EvalCommand(_conversationId, command, PageContext()), cancellationToken);
        await ExecuteAsync(runId, cancellationToken);
    }

    /// <summary>Sends the Undo request of the conversation and waits for the undo run.</summary>
    public async Task UndoAsync(CancellationToken cancellationToken)
    {
        var runId = await _agent.UndoAsync(_conversationId, cancellationToken);
        await EvalConversation.ActivateAsync(_services, _conversationId, runId, cancellationToken);
        await ExecuteAsync(runId, cancellationToken);
    }

    private async Task ExecuteAsync(Guid runId, CancellationToken cancellationToken)
    {
        await _agent.ExecuteAsync(runId, cancellationToken);
        var run = await _services.Runs.GetRunAsync(runId, cancellationToken);
        Failure = run?.Status == AgentRunStatusDto.Completed ? null : $"run {run?.Status}: {run?.FailureCode} {run?.FailureMessage}".Trim();
        _calls.AddRange((await _services.Runs.GetStepsAsync(runId, cancellationToken)).Select(Record));
    }

    private RecordedCall Record(RunStep step) =>
        new(step.Tool, Library.Ids.ToKeys(step.Arguments), step.Status == AgentStepStatusDto.Succeeded, step.Risk == ToolRiskDto.Read || step.Kind == ToolKindDto.Meta);

    private PageContextDto PageContext() => new() { Page = _case.Page, TrackId = Key(Library, _case.TrackId), MixId = Key(Library, _case.MixId) };

    private static Guid? Key(FixtureLibrary library, string? key) => key is null ? null : library.Ids.Of(key);

    /// <summary>Answers approval cards as the case states (none: reject), at once (the runner waits before the card is published).</summary>
    private void OnEvent(RunEvent runEvent)
    {
        if (runEvent.Type != AgentEventTypes.ApprovalRequested)
        {
            return;
        }

        Interlocked.Increment(ref _cards);
        var card = JsonSerializer.Deserialize<ApprovalRequestDto>(runEvent.Payload, Web)!;
        var decision = _case.Approval == ApprovalAnswer.Approve ? ApprovalDecisionDto.Approve : ApprovalDecisionDto.Reject;
        _ = DecideAsync(runEvent.RunId, card, decision);
    }

    private async Task DecideAsync(Guid runId, ApprovalRequestDto card, ApprovalDecisionDto decision)
    {
        await _agent.DecideAsync(runId, card.StepId, new DecideApprovalRequest { Decision = decision, ArgumentsHash = card.ArgumentsHash }, CancellationToken.None);
    }

    private AgentServices Services(LiveBackend backend) => new()
    {
        Runs = new MemoryRunStore(),
        Conversations = new MemoryConversationStore(),
        Settings = new FixedSettingsStore(AgentSettings.Defaults(backend.Backend, DateTimeOffset.UtcNow) with { OllamaModel = backend.Model }),
        Events = new EvalEventSink(OnEvent),
        Actions = new FixtureActions(Library),
        Ui = Page,
        Registry = Registry(),
        Chat = backend.Session(_case, Library.Ids),
        Undo = new MemoryUndoStore(),
    };

    private static AgentToolRegistry Registry() => new(
    [
        new MetaToolProvider(), new Core001ToolProvider(), new Detail002ToolProvider(), new Device003ToolProvider(), new HelpToolProvider(),
        new ArtistToolProvider(), new LyricsToolProvider(), new ExportToolProvider(), new ScoreToolProvider(), new StyleToolProvider(), new SongToolProvider(),
    ]);
}

/// <summary>An owner command of a case: conversation, text and page context.</summary>
/// <param name="ConversationId">The case's conversation.</param>
/// <param name="Text">The command text.</param>
/// <param name="Context">The page context.</param>
internal sealed record EvalCommand(Guid ConversationId, string Text, PageContextDto Context);

/// <summary>Creates the conversation records of an evaluation command, like the backend's <c>POST /api/agent/commands</c>.</summary>
internal static class EvalConversation
{
    /// <summary>Appends the user message, queues its run and marks it active; returns the run id.</summary>
    public static async Task<Guid> QueueAsync(AgentServices services, EvalCommand command, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var messages = await services.Conversations.GetMessagesAsync(command.ConversationId, cancellationToken);
        var message = new ConversationMessage { Id = Guid.NewGuid(), ConversationId = command.ConversationId, Seq = messages.Count + 1, Role = MessageRoleDto.User, Text = command.Text, Context = command.Context, RunId = Guid.NewGuid(), CreatedAt = now };
        await services.Conversations.AppendMessageAsync(message, cancellationToken);
        await services.Runs.CreateRunAsync(Run(message, (await services.Settings.GetAsync(cancellationToken)).Backend, now), cancellationToken);
        await ActivateAsync(services, command.ConversationId, message.RunId!.Value, cancellationToken);
        return message.RunId.Value;
    }

    /// <summary>Marks the run active in its conversation (created on first use).</summary>
    public static async Task ActivateAsync(AgentServices services, Guid conversationId, Guid runId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var conversation = await services.Conversations.GetAsync(conversationId, cancellationToken)
            ?? new Conversation { Id = conversationId, Title = "Evaluation", CreatedAt = now, UpdatedAt = now };
        await services.Conversations.UpdateAsync(conversation with { ActiveRunId = runId, UpdatedAt = now }, cancellationToken);
    }

    private static AgentRun Run(ConversationMessage message, AgentBackendDto backend, DateTimeOffset now) => new()
    {
        Id = message.RunId!.Value,
        ConversationId = message.ConversationId,
        UserMessageId = message.Id,
        Status = AgentRunStatusDto.Queued,
        Backend = backend,
        Model = string.Empty,
        Tainted = false,
        ModelTurns = 0,
        ToolCalls = 0,
        InvalidCalls = 0,
        StartedAt = now,
    };
}
