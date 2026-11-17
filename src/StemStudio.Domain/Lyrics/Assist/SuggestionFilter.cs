namespace StemStudio.Domain.Lyrics.Assist;

/// <summary>What the filter needs to know about the sheet and the assist.</summary>
/// <param name="Kind">Assist kind.</param>
/// <param name="Sensitivity">Sensitivity of the sheet.</param>
/// <param name="Text">Normalised text of the assist's revision.</param>
/// <param name="Marks">Intentional marks of the sheet.</param>
/// <param name="PersonalWords">Personal words of the sheet language.</param>
/// <param name="Language">Language.</param>
public sealed record FilterContext(
    AssistKind Kind, Sensitivity Sensitivity, string Text, IReadOnlyList<IntentionalMark> Marks, IReadOnlyCollection<string> PersonalWords, AssistLanguage Language);

/// <summary>
/// The deterministic server-side filter of every suggestion, dictionary and model alike (research R-03 steps 1 and 7, FR-009 - FR-012):
/// markers and blank lines are never touched, recognised licences are never errors, intentional marks and personal words are respected,
/// and a check shows by sensitivity (loose: errors; balanced: plus improvements; strict: plus ideas).
/// </summary>
public static class SuggestionFilter
{
    /// <summary>True when the suggestion may be shown.</summary>
    /// <param name="draft">The suggestion.</param>
    /// <param name="context">The context.</param>
    /// <returns>True to keep.</returns>
    public static bool Keep(SuggestionDraft draft, FilterContext context)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(context);
        if (draft.Anchor.IsTitle)
        {
            return draft.Category == SuggestionCategory.Title;
        }

        var lines = SheetText.Lines(context.Text);
        if (draft.Anchor.Line > lines.Length || !SheetText.IsLyric(lines[draft.Anchor.Line - 1]))
        {
            return false;
        }

        var line = lines[draft.Anchor.Line - 1];
        return !IsLicenceAsError(draft, context.Language) && !IsMarked(draft, line, context) && !IsPersonal(draft, context) && BySensitivity(draft, context);
    }

    private static bool IsLicenceAsError(SuggestionDraft draft, AssistLanguage language) =>
        draft.Severity == SuggestionSeverity.Error
        && (draft.Category == SuggestionCategory.Licence || LyricTokens.Of(draft.Anchor.Original) is { Count: > 0 } words && words.All(word => LicenceLexicon.TypeOf(word.Text, language) is not null));

    private static bool IsMarked(SuggestionDraft draft, string line, FilterContext context) =>
        context.Kind is AssistKind.Check or AssistKind.Devices && context.Marks.Any(mark => IntentionalMarkMatcher.Covers(mark, line, draft.Anchor));

    private static bool IsPersonal(SuggestionDraft draft, FilterContext context)
    {
        if (draft.Category != SuggestionCategory.Spelling)
        {
            return false;
        }

        var word = draft.Anchor.Original;
        return context.PersonalWords.Contains(word) || (word.Length > 0 && context.PersonalWords.Contains(char.ToLowerInvariant(word[0]) + word[1..]));
    }

    private static bool BySensitivity(SuggestionDraft draft, FilterContext context) => context.Kind != AssistKind.Check || context.Sensitivity switch
    {
        Sensitivity.Loose => draft.Severity == SuggestionSeverity.Error,
        Sensitivity.Balanced => draft.Severity != SuggestionSeverity.Idea,
        _ => true,
    };
}
