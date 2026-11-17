namespace StemStudio.Contracts.LyricAssist;

/// <summary>One anchored suggestion (schema <c>LyricSuggestion</c>; also the data of a <c>suggestion.added</c> event).</summary>
public sealed record LyricSuggestionDto
{
    /// <summary>Suggestion id.</summary>
    public required Guid SuggestionId { get; init; }

    /// <summary>The assist that produced it.</summary>
    public required Guid AssistId { get; init; }

    /// <summary>Category.</summary>
    public required SuggestionCategoryDto Category { get; init; }

    /// <summary>Type from the closed list.</summary>
    public required SuggestionTypeDto Type { get; init; }

    /// <summary>Severity.</summary>
    public required SuggestionSeverityDto Severity { get; init; }

    /// <summary>Where it applies.</summary>
    public required AnchorDto Anchor { get; init; }

    /// <summary>What accepting it does.</summary>
    public required SuggestionOperationDto Operation { get; init; }

    /// <summary>Proposed text (at most 400 characters; insertions up to 4 LF-separated lines); null for <c>none</c>.</summary>
    public string? Proposal { get; init; }

    /// <summary>Alternatives of one line share a group; accepting one supersedes the others.</summary>
    public Guid? GroupId { get; init; }

    /// <summary>Plain-text explanation in the sheet's language (1-300 characters).</summary>
    public required string Explanation { get; init; }

    /// <summary>Layer that produced it.</summary>
    public required SuggestionSourceDto Source { get; init; }

    /// <summary>Model name when <see cref="Source"/> is <c>model</c>.</summary>
    public string? Model { get; init; }

    /// <summary>Syllables of the line before (computed, never the model's claim).</summary>
    public int? SyllablesBefore { get; init; }

    /// <summary>Syllables of the line after accepting (computed).</summary>
    public int? SyllablesAfter { get; init; }

    /// <summary>Verified rhyme partner of a rhyme claim.</summary>
    public RhymePartnerDto? RhymeWith { get; init; }

    /// <summary>Lifecycle state.</summary>
    public required SuggestionStatusDto Status { get; init; }

    /// <summary>When it was accepted, rejected or marked intentional.</summary>
    public DateTimeOffset? DecidedAt { get; init; }
}
