namespace StemStudio.Contracts.Library;

/// <summary>Response of <c>GET /api/library/search</c> (backend-api 1.22.0 <c>LibrarySearchResult</c>).</summary>
public sealed record LibrarySearchResultDto
{
    /// <summary>The searched text (trimmed, cut to 100 characters).</summary>
    public required string Query { get; init; }

    /// <summary>The folded words that were matched (1..8).</summary>
    public IReadOnlyList<string> Tokens { get; init; } = [];

    /// <summary>Library revision (16 hex).</summary>
    public required string Revision { get; init; }

    /// <summary>Artist hits (at most <c>limit</c>).</summary>
    public IReadOnlyList<FolderHitDto> Artists { get; init; } = [];

    /// <summary>Album hits.</summary>
    public IReadOnlyList<FolderHitDto> Albums { get; init; } = [];

    /// <summary>Song hits.</summary>
    public IReadOnlyList<FolderHitDto> Songs { get; init; } = [];

    /// <summary>Item hits.</summary>
    public IReadOnlyList<ItemHitDto> Items { get; init; } = [];

    /// <summary>Hits per group in total.</summary>
    public required SearchTotalsDto Totals { get; init; }
}
