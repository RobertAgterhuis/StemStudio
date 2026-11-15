namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/conversations/{conversationId}/messages: one command.</summary>
public sealed record SendAgentMessageRequest
{
    /// <summary>The command, 1-4,000 characters.</summary>
    public required string Text { get; init; }

    /// <summary>Page the command was typed on.</summary>
    public required PageContextDto Context { get; init; }
}
