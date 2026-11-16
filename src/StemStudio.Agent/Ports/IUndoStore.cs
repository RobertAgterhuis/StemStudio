using StemStudio.Agent.Model;

namespace StemStudio.Agent.Ports;

/// <summary>Undo entries (data-model 4.4). Implemented by the backend (T021).</summary>
public interface IUndoStore
{
    /// <summary>Stores the undo entries recorded for executed steps.</summary>
    /// <param name="entries">Entries of one run.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task AddAsync(IReadOnlyList<UndoEntry> entries, CancellationToken cancellationToken);

    /// <summary>Available entries of a conversation, newest run first, steps in reverse order.</summary>
    /// <param name="conversationId">Conversation identifier.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The entries.</returns>
    Task<IReadOnlyList<UndoEntry>> GetAvailableAsync(Guid conversationId, CancellationToken cancellationToken);

    /// <summary>Marks an entry applied or obsolete.</summary>
    /// <param name="entryId">Entry identifier.</param>
    /// <param name="status">New status.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    Task SetStatusAsync(Guid entryId, UndoEntryStatus status, CancellationToken cancellationToken);
}
