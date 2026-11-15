namespace StemStudio.Contracts.Agent;

/// <summary>A conversation with its messages and run summaries (GET /api/agent/conversations/{conversationId}).</summary>
public sealed record ConversationDto : ConversationSummaryDto
{
    /// <summary>Messages in order of <see cref="AgentMessageDto.Seq"/>.</summary>
    public IReadOnlyList<AgentMessageDto> Messages { get; init; } = [];

    /// <summary>Runs in start order.</summary>
    public IReadOnlyList<AgentRunSummaryDto> Runs { get; init; } = [];
}
