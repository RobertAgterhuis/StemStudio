using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>A validated model suggestion with a verified anchor; the backend adds ids, status and persistence.</summary>
public sealed record AssistantSuggestion
{
    /// <summary>Category (derived from the type).</summary>
    public required SuggestionCategoryDto Category { get; init; }

    /// <summary>Type.</summary>
    public required SuggestionTypeDto Type { get; init; }

    /// <summary>Severity.</summary>
    public required SuggestionSeverityDto Severity { get; init; }

    /// <summary>Verified anchor in the job's text.</summary>
    public required AnchorDto Anchor { get; init; }

    /// <summary>What accepting it does.</summary>
    public required SuggestionOperationDto Operation { get; init; }

    /// <summary>Proposed text; null for <c>none</c>.</summary>
    public string? Proposal { get; init; }

    /// <summary>Alternatives of one line share a group id.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Plain-text explanation in the sheet's language.</summary>
    public required string Explanation { get; init; }

    /// <summary>Computed syllables of the line before.</summary>
    public int? SyllablesBefore { get; init; }

    /// <summary>Computed syllables of the line after accepting.</summary>
    public int? SyllablesAfter { get; init; }

    /// <summary>Verified rhyme partner.</summary>
    public RhymePartnerDto? RhymeWith { get; init; }
}
