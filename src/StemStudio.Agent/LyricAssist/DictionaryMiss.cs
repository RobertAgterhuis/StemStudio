namespace StemStudio.Agent.LyricAssist;

/// <summary>A word the dictionaries, the personal list and the licence rules did not accept; the model classifies it.</summary>
public sealed record DictionaryMiss
{
    /// <summary>1-based line.</summary>
    public required int Line { get; init; }

    /// <summary>UTF-16 offset in the line.</summary>
    public required int Start { get; init; }

    /// <summary>The word as written.</summary>
    public required string Word { get; init; }

    /// <summary>Dictionary suggestions, best first.</summary>
    public IReadOnlyList<string> Suggestions { get; init; } = [];
}
