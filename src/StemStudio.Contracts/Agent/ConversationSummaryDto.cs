namespace StemStudio.Contracts.Agent;

/// <summary>Entry of the conversation list (GET /api/agent/conversations), newest first.</summary>
public record ConversationSummaryDto
{
    /// <summary>Conversation identifier.</summary>
    public required Guid ConversationId { get; init; }

    /// <summary>Title, at most 120 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Time of the last message or run change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Status of the newest run, null when the conversation has no run.</summary>
    public AgentRunStatusDto? LastRunStatus { get; init; }
}
