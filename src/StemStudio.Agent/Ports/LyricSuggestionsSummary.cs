namespace StemStudio.Agent.Ports;

/// <summary>Suggestions of a sheet without any text (list_lyric_suggestions).</summary>
public sealed record LyricSuggestionsSummary
{
    /// <summary>Current sheet revision.</summary>
    public required int Revision { get; init; }

    /// <summary>The suggestions.</summary>
    public required IReadOnlyList<LyricSuggestionRef> Items { get; init; }
}
