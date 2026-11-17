using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>One parsed <c>items[]</c> entry of the model output (<c>$defs/item</c> of lyric-assist-output 1.0.0); nothing here is trusted.</summary>
public sealed record ModelSuggestionItem
{
    /// <summary>Type.</summary>
    public required SuggestionTypeDto Type { get; init; }

    /// <summary>Severity.</summary>
    public required SuggestionSeverityDto Severity { get; init; }

    /// <summary>1-based line; 0 for titles.</summary>
    public required int Line { get; init; }

    /// <summary>Exact text being changed (empty for a continuation; the title for a title idea).</summary>
    public required string Original { get; init; }

    /// <summary>Proposed text.</summary>
    public string? Proposal { get; init; }

    /// <summary>Explanation (1-300 characters).</summary>
    public required string Explanation { get; init; }

    /// <summary>Claimed rhyme partner line.</summary>
    public int? RhymeWithLine { get; init; }

    /// <summary>Claimed rhyme partner word.</summary>
    public string? RhymeWord { get; init; }
}
