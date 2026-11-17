namespace StemStudio.Contracts.LyricAssist;

/// <summary>A line that came from accepted assistant text (schema <c>AssistedLine</c>).</summary>
public sealed record AssistedLineDto
{
    /// <summary>1-based line.</summary>
    public required int Line { get; init; }

    /// <summary>The accepted suggestion.</summary>
    public required Guid SuggestionId { get; init; }
}
