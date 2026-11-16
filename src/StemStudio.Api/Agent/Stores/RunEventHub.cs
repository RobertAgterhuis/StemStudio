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
/// run (appends are serialised) and <see cref="IRunEventLogReader"/> replays them.
/// </summary>
/// <param name="db">Context per operation.</param>
/// <param name="clock">Clock for the creation times.</param>
public sealed class RunEventHub(AgentDb db, TimeProvider clock) : IRunEventSink, IRunEventLogReader, IDisposable
{
    private readonly SemaphoreSlim _appendLock = new(1, 1);

    /// <inheritdoc />
    public async Task<RunEvent> PublishAsync(Guid runId, string type, string payload, CancellationToken cancellationToken)
    {
        await _appendLock.WaitAsync(cancellationToken);
        try
        {
            var entity = await db.WithAsync(context => new RunEventLog(context).AppendAsync(runId, type, payload, clock.GetUtcNow().UtcDateTime, cancellationToken));
            return ToRecord(entity);
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

    private static RunEvent ToRecord(RunEventEntity entity) => new()
    {
        RunId = entity.RunId,
        Seq = entity.Seq,
        Type = entity.Type,
        Payload = entity.Payload,
        CreatedAt = Offset(entity.CreatedAt),
    };
}
