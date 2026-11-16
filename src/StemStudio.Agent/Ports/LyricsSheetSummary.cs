namespace StemStudio.Agent.Ports;

/// <summary>A saved lyrics sheet without its text (FR-026): what list_lyrics_sheets and save_lyrics_sheet may show.</summary>
public sealed record LyricsSheetSummary
{
    /// <summary>Sheet id.</summary>
    public required Guid LyricsId { get; init; }

    /// <summary>Title (the user's own text; projected as <c>untrusted_text</c>).</summary>
    public required string Title { get; init; }

    /// <summary>Language code.</summary>
    public required string Language { get; init; }

    /// <summary>Last change (UTC).</summary>
    public required DateTimeOffset UpdatedAt { get; init; }
}
