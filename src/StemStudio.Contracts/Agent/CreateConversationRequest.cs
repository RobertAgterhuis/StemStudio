namespace StemStudio.Contracts.Agent;

/// <summary>Body of POST /api/agent/conversations.</summary>
public sealed record CreateConversationRequest
{
    /// <summary>Title, at most 120 characters; default: the first 60 characters of the first message.</summary>
    public string? Title { get; init; }
}
