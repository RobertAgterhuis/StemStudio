namespace StemStudio.Contracts.LyricAssist;

/// <summary>An assist with its suggestions (schema <c>LyricAssist</c> = <c>LyricAssistSummary</c> + <c>suggestions</c>).</summary>
public sealed record LyricAssistDto : LyricAssistSummaryDto
{
    /// <summary>Suggestions (at most 400).</summary>
    public required IReadOnlyList<LyricSuggestionDto> Suggestions { get; init; }
}
