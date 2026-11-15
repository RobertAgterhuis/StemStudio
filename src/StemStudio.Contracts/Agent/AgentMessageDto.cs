namespace StemStudio.Contracts.Agent;

/// <summary>One message of a conversation.</summary>
public sealed record AgentMessageDto
{
    /// <summary>Message identifier.</summary>
    public required Guid MessageId { get; init; }

    /// <summary>Position in the conversation, from 1.</summary>
    public required int Seq { get; init; }

    /// <summary>Author.</summary>
    public required MessageRoleDto Role { get; init; }

    /// <summary>Text (user at most 4,000 characters, assistant at most 16,000).</summary>
    public required string Text { get; init; }

    /// <summary>Page context of the command (assistant messages copy it).</summary>
    public required PageContextDto Context { get; init; }

    /// <summary>Run started by (user) or answered by (assistant) this message.</summary>
    public Guid? RunId { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }
}
