namespace StemStudio.Contracts.Agent;

/// <summary>Body of PATCH /api/agent/conversations/{conversationId}.</summary>
public sealed record RenameConversationRequest
{
    /// <summary>New title, 1-120 characters after trimming.</summary>
    public required string Title { get; init; }
}
