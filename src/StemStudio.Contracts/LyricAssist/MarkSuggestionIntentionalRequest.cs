namespace StemStudio.Contracts.LyricAssist;

/// <summary>Body of <c>POST .../suggestions/{suggestionId}/intentional</c>.</summary>
public sealed record MarkSuggestionIntentionalRequest
{
    /// <summary><c>word</c>/<c>phrase</c> = the anchor text; <c>line</c> = the whole anchor line.</summary>
    public required IntentionalScopeDto Scope { get; init; }

    /// <summary>The revision the editor shows.</summary>
    public required int ExpectedRevision { get; init; }

    /// <summary>Optional note (at most 120 characters).</summary>
    public string? Note { get; init; }
}
