namespace StemStudio.Contracts.InstrumentViews;

/// <summary><c>GET .../chords/capo-suggestions</c> (<c>CapoSuggestions</c>) (backend-api 1.15.0, feature 016).</summary>
public sealed record CapoSuggestionsDto
{
    /// <summary>Requested transpose (-12..12).</summary>
    public required int Transpose { get; init; }

    /// <summary>The original-shapes shortcut, or null.</summary>
    public required CapoShortcutDto? Shortcut { get; init; }

    /// <summary>Up to three suggestions, best first.</summary>
    public required IReadOnlyList<CapoSuggestionDto> Suggestions { get; init; }
}
