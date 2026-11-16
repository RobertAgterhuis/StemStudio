using System.Collections.Concurrent;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;

namespace StemStudio.AgentEval.Fixture;

/// <summary>In-memory run store of one evaluation run (what the backend's SQLite stores hold in the app).</summary>
internal sealed class MemoryRunStore : IRunStore
{
    private readonly ConcurrentDictionary<Guid, AgentRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, RunStep> _steps = new();
    private readonly ConcurrentDictionary<Guid, Approval> _approvals = new();

    public Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken) => Set(_runs, run.Id, run);

    public Task<AgentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult(_runs.GetValueOrDefault(runId));

    public Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken) => Set(_runs, run.Id, run);

    public Task<AgentRun?> GetActiveRunAsync(CancellationToken cancellationToken) => Task.FromResult(_runs.Values.FirstOrDefault(run => !run.IsTerminal));

    public Task SaveStepAsync(RunStep runStep, CancellationToken cancellationToken) => Set(_steps, runStep.Id, runStep);

    public Task<IReadOnlyList<RunStep>> GetStepsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RunStep>>([.. _steps.Values.Where(step => step.RunId == runId).OrderBy(step => step.Seq)]);

    public Task SaveApprovalAsync(Approval approval, CancellationToken cancellationToken) => Set(_approvals, approval.Id, approval);

    public Task<Approval?> GetApprovalAsync(Guid stepId, CancellationToken cancellationToken) => Task.FromResult(_approvals.GetValueOrDefault(stepId));

    private static Task Set<T>(ConcurrentDictionary<Guid, T> items, Guid id, T item)
    {
        items[id] = item;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory conversations and messages.</summary>
internal sealed class MemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, Conversation> _conversations = new();
    private readonly ConcurrentQueue<ConversationMessage> _messages = new();

    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken) => UpdateAsync(conversation, cancellationToken);

    public Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken) => Task.FromResult(_conversations.GetValueOrDefault(conversationId));

    public Task<IReadOnlyList<Conversation>> ListAsync(int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Conversation>>([.. _conversations.Values.Take(limit)]);

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        _conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken) => Task.FromResult(_conversations.TryRemove(conversationId, out _));

    public Task AppendMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ConversationMessage>>([.. _messages.Where(message => message.ConversationId == conversationId).OrderBy(message => message.Seq)]);
}

/// <summary>In-memory undo entries (available entries newest first, like the backend store).</summary>
internal sealed class MemoryUndoStore : IUndoStore
{
    private readonly List<UndoEntry> _entries = [];
    private readonly Lock _gate = new();

    public Task AddAsync(IReadOnlyList<UndoEntry> entries, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            _entries.AddRange(entries);
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<UndoEntry>> GetAvailableAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<UndoEntry>>([.. _entries.Where(entry => entry.ConversationId == conversationId && entry.Status == UndoEntryStatus.Available).Reverse()]);
        }
    }

    public Task SetStatusAsync(Guid entryId, UndoEntryStatus status, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var index = _entries.FindIndex(entry => entry.Id == entryId);
            _entries[index] = _entries[index] with { Status = status };
        }

        return Task.CompletedTask;
    }
}

/// <summary>Fixed agent settings of the evaluation (backend and model under test).</summary>
internal sealed class FixedSettingsStore(AgentSettings settings) : IAgentSettingsStore
{
    public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(settings);

    public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>The event stream of a run; hands approval cards to the harness.</summary>
internal sealed class EvalEventSink(Action<RunEvent> onEvent) : IRunEventSink
{
    private int _seq;

    public Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken)
    {
        var stored = new RunEvent { RunId = runId, Seq = Interlocked.Increment(ref _seq), Type = type, Payload = payload, CreatedAt = DateTimeOffset.UtcNow };
        onEvent(stored);
        return Task.FromResult(stored);
    }
}

/// <summary>No GPU job runs during the evaluation (the primary model is used).</summary>
internal sealed class IdleGpu : IGpuActivity
{
    public bool IsHeavyJobActive() => false;
}
