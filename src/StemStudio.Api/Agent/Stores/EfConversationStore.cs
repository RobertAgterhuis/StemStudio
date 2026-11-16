using Microsoft.EntityFrameworkCore;
using StemStudio.Agent.Model;
using StemStudio.Agent.Ports;
using static StemStudio.Api.Agent.Stores.AgentStoreValues;

namespace StemStudio.Api.Agent.Stores;

/// <summary>EF Core implementation of <see cref="IConversationStore"/> (feature 011, T021).</summary>
/// <param name="db">Context per operation.</param>
public sealed class EfConversationStore(AgentDb db) : IConversationStore
{
    /// <inheritdoc />
    public Task CreateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return db.SaveAsync(context => Task.FromResult(context.AgentConversations.Add(ConversationMapping.Apply(new(), conversation))), cancellationToken);
    }

    /// <inheritdoc />
    public Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken) => db.WithAsync(async context =>
    {
        var entity = await context.AgentConversations.AsNoTracking().SingleOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        return entity is null ? null : ConversationMapping.ToRecord(entity);
    });

    /// <inheritdoc />
    public Task<IReadOnlyList<Conversation>> ListAsync(int limit, DateTimeOffset? before, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<Conversation>>(async context =>
        {
            var query = context.AgentConversations.AsNoTracking();
            if (before is { } cutoff)
            {
                var utc = Utc(cutoff);
                query = query.Where(c => c.UpdatedAt < utc);
            }

            var page = await query.OrderByDescending(c => c.UpdatedAt).Take(limit).ToListAsync(cancellationToken);
            return [.. page.Select(ConversationMapping.ToRecord)];
        });

    /// <inheritdoc />
    public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        return db.SaveAsync(async context =>
        {
            var entity = await context.AgentConversations.SingleOrDefaultAsync(c => c.Id == conversation.Id, cancellationToken)
                ?? throw new KeyNotFoundException($"Conversation {conversation.Id} does not exist.");
            ConversationMapping.Apply(entity, conversation);
        }, cancellationToken);
    }

    /// <inheritdoc />
    public Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken) => db.WithAsync(async context =>
        await context.AgentConversations.Where(c => c.Id == conversationId).ExecuteDeleteAsync(cancellationToken) > 0);

    /// <inheritdoc />
    public Task AppendMessageAsync(ConversationMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        return db.SaveAsync(context => Task.FromResult(context.AgentMessages.Add(ConversationMapping.ToEntity(message))), cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken) =>
        db.WithAsync<IReadOnlyList<ConversationMessage>>(async context =>
        {
            var rows = await context.AgentMessages.AsNoTracking()
                .Where(m => m.ConversationId == conversationId).OrderBy(m => m.Seq).ToListAsync(cancellationToken);
            return [.. rows.Select(ConversationMapping.ToRecord)];
        });
}
