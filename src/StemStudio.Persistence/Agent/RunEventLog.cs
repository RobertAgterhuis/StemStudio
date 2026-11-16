using Microsoft.EntityFrameworkCore;

namespace StemStudio.Persistence.Agent;

/// <summary>Appends and replays run events (feature 011, SSE replay with <c>after</c> / <c>Last-Event-ID</c>).</summary>
/// <param name="context">The database context.</param>
public sealed class RunEventLog(StemStudioDbContext context)
{
    /// <summary>Appends an event with the next Seq of its run. Callers serialise appends per run.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="type">Event type.</param>
    /// <param name="payload">JSON data.</param>
    /// <param name="createdAt">Creation time (UTC).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The stored event.</returns>
    public async Task<RunEventEntity> AppendAsync(Guid runId, string type, string payload, DateTime createdAt, CancellationToken cancellationToken)
    {
        var last = await context.AgentRunEvents.Where(e => e.RunId == runId).MaxAsync(e => (int?)e.Seq, cancellationToken);
        var entity = new RunEventEntity { RunId = runId, Seq = (last ?? 0) + 1, Type = type, Payload = payload, CreatedAt = createdAt };
        context.AgentRunEvents.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity;
    }

    /// <summary>Events of a run with a Seq larger than <paramref name="after"/>, in Seq order.</summary>
    /// <param name="runId">Run id.</param>
    /// <param name="after">Last Seq the client has (0 = all).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The events.</returns>
    public async Task<IReadOnlyList<RunEventEntity>> ListAfterAsync(Guid runId, int after, CancellationToken cancellationToken) =>
        await context.AgentRunEvents.AsNoTracking()
            .Where(e => e.RunId == runId && e.Seq > after)
            .OrderBy(e => e.Seq)
            .ToListAsync(cancellationToken);
}
