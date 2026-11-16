using Microsoft.EntityFrameworkCore;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;

namespace StemStudio.Api.Agent.Stores;

/// <summary>EF Core implementation of <see cref="IUndoStore"/> (feature 011, T021): newest entry first.</summary>
/// <param name="db">Context per operation.</param>
public sealed class EfUndoStore(AgentDb db) : IUndoStore
{
    private static readonly string Available = nameof(UndoEntryStatus.Available);

    /// <inheritdoc />
    public Task AddAsync(IReadOnlyList<UndoEntry> entries, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entries);
        return db.SaveAsync(async context =>
        {
            var conversationIds = entries.Select(e => e.ConversationId).Distinct().ToList();
            var last = await context.AgentUndoEntries.Where(e => conversationIds.Contains(e.ConversationId))
                .MaxAsync(e => (int?)e.Ordinal, cancellationToken) ?? 0;
            context.AgentUndoEntries.AddRange(entries.Select((entry, index) => StepMapping.ToEntity(entry, last + index + 1)));
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<UndoEntry>> GetAvailableAsync(Guid conversationId, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<UndoEntry>>(async context =>
        {
            var rows = await context.AgentUndoEntries.AsNoTracking()
                .Where(e => e.ConversationId == conversationId && e.Status == Available)
                .OrderByDescending(e => e.Ordinal).ToListAsync(cancellationToken);
            return [.. rows.Select(StepMapping.ToRecord)];
        });

    /// <inheritdoc />
    public Task SetStatusAsync(Guid entryId, UndoEntryStatus status, CancellationToken cancellationToken) => db.SaveAsync(async context =>
    {
        var entity = await context.AgentUndoEntries.SingleOrDefaultAsync(e => e.Id == entryId, cancellationToken)
            ?? throw new KeyNotFoundException($"Undo entry {entryId} does not exist.");
        entity.Status = status.ToString();
    }, cancellationToken);
}
