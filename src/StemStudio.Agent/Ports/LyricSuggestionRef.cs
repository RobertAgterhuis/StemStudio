using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Ports;

/// <summary>One suggestion without original, proposal or explanation text.</summary>
public sealed record LyricSuggestionRef
{
    /// <summary>Suggestion id.</summary>
    public required Guid SuggestionId { get; init; }

    /// <summary>Category.</summary>
    public required SuggestionCategoryDto Category { get; init; }

    /// <summary>Type.</summary>
    public required SuggestionTypeDto Type { get; init; }

    /// <summary>Severity.</summary>
    public required SuggestionSeverityDto Severity { get; init; }

    /// <summary>1-based line (0 for titles).</summary>
    public required int Line { get; init; }

    /// <summary>Status.</summary>
    public required SuggestionStatusDto Status { get; init; }

    /// <summary>Layer.</summary>
    public required SuggestionSourceDto Source { get; init; }
}
