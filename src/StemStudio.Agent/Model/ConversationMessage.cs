using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Model;

/// <summary>One message of a conversation; user messages are immutable (data-model 3).</summary>
public sealed record ConversationMessage
{
    /// <summary>Message identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Conversation identifier.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>Monotonic position from 1.</summary>
    public required int Seq { get; init; }

    /// <summary>Author.</summary>
    public required MessageRoleDto Role { get; init; }

    /// <summary>Text (user at most 4,000, assistant at most 16,000 characters).</summary>
    public required string Text { get; init; }

    /// <summary>Page context of the command.</summary>
    public required PageContextDto Context { get; init; }

    /// <summary>Run started or answered by this message.</summary>
    public Guid? RunId { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
