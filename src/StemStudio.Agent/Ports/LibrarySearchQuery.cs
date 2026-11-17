namespace StemStudio.Agent.Ports;

/// <summary>Arguments of GET /api/library/search used by search_library_items (tool catalogue 1.1.0).</summary>
public sealed record LibrarySearchQuery
{
    /// <summary>The search text (1..100 characters).</summary>
    public required string Query { get; init; }

    /// <summary>Only folders and items inside this folder; null = the whole library.</summary>
    public string? Within { get; init; }

    /// <summary>Hits per group (the tool asks for 10).</summary>
    public int? Limit { get; init; }
}
