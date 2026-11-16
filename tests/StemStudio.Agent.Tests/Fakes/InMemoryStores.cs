using System.Collections.Concurrent;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Tests.Fakes;

/// <summary>In-memory <see cref="IRunStore"/> (thread-safe, keeps the latest version of every record).</summary>
public sealed class InMemoryRunStore : IRunStore
{
    private readonly ConcurrentDictionary<Guid, AgentRun> _runs = new();
    private readonly ConcurrentDictionary<Guid, RunStep> _steps = new();
    private readonly ConcurrentDictionary<Guid, Approval> _approvals = new();

    public IReadOnlyList<RunStep> Steps => [.. _steps.Values.OrderBy(step => step.Seq)];

    public Task CreateRunAsync(AgentRun run, CancellationToken cancellationToken)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetRunAsync(Guid runId, CancellationToken cancellationToken) => Task.FromResult(_runs.GetValueOrDefault(runId));

    public Task UpdateRunAsync(AgentRun run, CancellationToken cancellationToken)
    {
        _runs[run.Id] = run;
        return Task.CompletedTask;
    }

    public Task<AgentRun?> GetActiveRunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_runs.Values.FirstOrDefault(run => !run.IsTerminal));

    public Task SaveStepAsync(RunStep runStep, CancellationToken cancellationToken)
    {
        _steps[runStep.Id] = runStep;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RunStep>> GetStepsAsync(Guid runId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RunStep>>([.. _steps.Values.Where(step => step.RunId == runId).OrderBy(step => step.Seq)]);

    public Task SaveApprovalAsync(Approval approval, CancellationToken cancellationToken)
    {
        _approvals[approval.Id] = approval;
        return Task.CompletedTask;
    }

    public Task<Approval?> GetApprovalAsync(Guid stepId, CancellationToken cancellationToken) => Task.FromResult(_approvals.GetValueOrDefault(stepId));
}

/// <summary>In-memory <see cref="IConversationStore"/>.</summary>
public sealed class InMemoryConversationStore : IConversationStore
{
    private readonly ConcurrentDictionary<Guid, Conversation> _conversations = new();
    private readonly ConcurrentQueue<ConversationMessage> _messages = new();

    public IReadOnlyList<ConversationMessage> Messages => [.. _messages.OrderBy(message => message.Seq)];

    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        _conversations[conversation.Id] = conversation;
        return Task.CompletedTask;
    }

    public Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken) => Task.FromResult(_conversations.GetValueOrDefault(conversationId));

    public Task<IReadOnlyList<Conversation>> ListAsync(int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Conversation>>([.. _conversations.Values.Take(limit)]);

    public int Updates { get; private set; }

    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        Updates++;
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

/// <summary>In-memory <see cref="IAgentSettingsStore"/>.</summary>
/// <param name="settings">Initial settings.</param>
public sealed class InMemorySettingsStore(AgentSettings settings) : IAgentSettingsStore
{
    public AgentSettings Current { get; private set; } = settings;

    public Task<AgentSettings> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Current);

    public Task SaveAsync(AgentSettings settings, CancellationToken cancellationToken)
    {
        Current = settings;
        return Task.CompletedTask;
    }
}

/// <summary>In-memory <see cref="IUndoStore"/>.</summary>
public sealed class InMemoryUndoStore : IUndoStore
{
    private readonly List<UndoEntry> _entries = [];
    private readonly Lock _gate = new();

    public IReadOnlyList<UndoEntry> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

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
            var order = _entries.Select(entry => entry.RunId).Distinct().ToList();
            IReadOnlyList<UndoEntry> available = [.. _entries
                .Where(entry => entry.ConversationId == conversationId && entry.Status == UndoEntryStatus.Available)
                .OrderByDescending(entry => order.IndexOf(entry.RunId)).ThenByDescending(entry => _entries.IndexOf(entry))];
            return Task.FromResult(available);
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
