namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of accept and reject (schema <c>SuggestionDecisionRequest</c>).</summary>
public sealed record SuggestionDecisionRequest
{
    /// <summary>1-100 distinct suggestion ids.</summary>
    public required IReadOnlyList<Guid> SuggestionIds { get; init; }

    /// <summary>The revision the editor shows.</summary>
    public required int ExpectedRevision { get; init; }
}
