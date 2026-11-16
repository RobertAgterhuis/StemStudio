namespace StemStudio.Agent.Model;

/// <summary>A conversation of the agent panel (data-model 3).</summary>
public sealed record Conversation
{
    /// <summary>Conversation identifier.</summary>
    public required Guid Id { get; init; }

    /// <summary>Title, at most 120 characters.</summary>
    public required string Title { get; init; }

    /// <summary>Creation time (UTC).</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>The non-terminal run of the conversation, when any.</summary>
    public Guid? ActiveRunId { get; init; }
}
