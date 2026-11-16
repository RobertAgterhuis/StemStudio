using System.Collections.Concurrent;
using System.Threading.Channels;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using StemStudio.Persistence.Agent;
using static StemStudio.Api.Agent.Stores.AgentStoreValues;

namespace StemStudio.Api.Agent.Stores;

/// <summary>Reads the persisted event log of a run (SSE replay).</summary>
public interface IRunEventLogReader
{
    /// <summary>Events with a Seq larger than <paramref name="after"/>, in Seq order.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="after">Last Seq the client has (0 = all).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The events.</returns>
    Task<IReadOnlyList<RunEvent>> ReadAfterAsync(Guid runId, int after, CancellationToken cancellationToken);
}

/// <summary>
/// The run event log (feature 011, data-model 4.5): <see cref="IRunEventSink"/> persists each event with the next Seq of its
/// run (appends are serialised), pushes it to the live subscribers of the run (all SSE clients, e.g. two tabs), and
/// <see cref="IRunEventLogReader"/> replays them.
/// </summary>
/// <param name="db">Context per operation.</param>
/// <param name="clock">Clock for the creation times.</param>
public sealed class RunEventHub(AgentDb db, TimeProvider clock) : IRunEventSink, IRunEventLogReader, IDisposable
{
    private readonly SemaphoreSlim _appendLock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, ConcurrentDictionary<Channel<RunEvent>, byte>> _subscribers = new();

    /// <summary>Subscribes to the live events of a run; dispose the subscription to stop.</summary>
    /// <param name="runId">Run id.</param>
    /// <returns>The subscription.</returns>
    public RunEventSubscription Subscribe(Guid runId)
    {
        var channel = Channel.CreateUnbounded<RunEvent>(new UnboundedChannelOptions { SingleReader = true });
        var channels = _subscribers.GetOrAdd(runId, _ => new());
        channels[channel] = 0;
        return new RunEventSubscription(channel.Reader, () => channels.TryRemove(channel, out _));
    }

    /// <inheritdoc />
    public async Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken)
    {
        await _appendLock.WaitAsync(cancellationToken);
        try
        {
            var entity = await db.WithAsync(context => new RunEventLog(context).AppendAsync(
                new RunEventEntity { RunId = runId, Type = type, Payload = payload, CreatedAt = clock.GetUtcNow().UtcDateTime }, cancellationToken));
            var runEvent = ToRecord(entity);
            Broadcast(runEvent);
            return runEvent;
        }
        finally
        {
            _appendLock.Release();
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<RunEvent>> ReadAfterAsync(Guid runId, int after, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<RunEvent>>(async context =>
            [.. (await new RunEventLog(context).ListAfterAsync(runId, after, cancellationToken)).Select(ToRecord)]);

    /// <inheritdoc />
    public void Dispose() => _appendLock.Dispose();

    private void Broadcast(RunEvent runEvent)
    {
        if (_subscribers.TryGetValue(runEvent.RunId, out var channels))
        {
            foreach (var channel in channels.Keys)
            {
                channel.Writer.TryWrite(runEvent);
            }
        }
    }

    private static RunEvent ToRecord(RunEventEntity entity) => new()
    {
        RunId = entity.RunId,
        Seq = entity.Seq,
        Type = entity.Type,
        Payload = entity.Payload,
        CreatedAt = Offset(entity.CreatedAt),
    };
}

/// <summary>A live subscription to the events of one run.</summary>
/// <param name="reader">The events published after subscribing.</param>
/// <param name="unsubscribe">Removes the subscription.</param>
public sealed class RunEventSubscription(ChannelReader<RunEvent> reader, Action unsubscribe) : IDisposable
{
    /// <summary>The events published after subscribing.</summary>
    public ChannelReader<RunEvent> Events { get; } = reader;

    /// <inheritdoc />
    public void Dispose() => unsubscribe();
}
