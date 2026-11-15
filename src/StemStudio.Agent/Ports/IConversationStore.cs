using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>Conversations and messages (data-model 3). Implemented by the backend (EfConversationStore, T021).</summary>
public interface IConversationStore
{
    /// <summary>Stores a new conversation.</summary>
    /// <param name="conversation">The conversation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task CreateAsync(Conversation conversation, CancellationToken cancellationToken);

    /// <summary>Loads a conversation.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The conversation, or null.</returns>
    Task<Conversation?> GetAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Lists conversations, newest <see cref="Conversation.UpdatedAt"/> first.</summary>
    /// <param name="limit">Page size, 1-100.</param>
    /// <param name="before">Only conversations updated before this time.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The page.</returns>
    Task<IReadOnlyList<Conversation>> ListAsync(int limit, DateTimeOffset? before, CancellationToken cancellationToken);

    /// <summary>Replaces title, times and the active run.</summary>
    /// <param name="conversation">The changed conversation.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken);

    /// <summary>Deletes a conversation with its messages, runs, steps, approvals, undo entries and events.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>False when it did not exist.</returns>
    Task<bool> DeleteAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Appends a message; the store assigns nothing (Seq is set by the caller, monotonic).</summary>
    /// <param name="message">The message.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AppendMessageAsync(ConversationMessage message, CancellationToken cancellationToken);

    /// <summary>Messages of a conversation in Seq order.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The messages.</returns>
    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid conversationId, CancellationToken cancellationToken);
}
