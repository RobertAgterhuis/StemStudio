namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>GET /api/lyrics/{lyricsId}/suggestions</c> and of a reject (schema <c>SuggestionList</c>).</summary>
public sealed record SuggestionListDto
{
    /// <summary>The sheet.</summary>
    public required Guid LyricsId { get; init; }

    /// <summary>Current sheet revision.</summary>
    public required int Revision { get; init; }

    /// <summary>Suggestions (at most 400), filtered as requested.</summary>
    public required IReadOnlyList<LyricSuggestionDto> Suggestions { get; init; }
}
