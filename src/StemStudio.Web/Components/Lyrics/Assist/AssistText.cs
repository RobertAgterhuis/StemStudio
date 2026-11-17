using System.Globalization;
using System.Text.Json;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Web.Components.Lyrics.Assist;

/// <summary>
/// Readable English texts of the assistant pane (UI text stays English, MoSCoW W7) and the live-region announcements of
/// ui-test-ids 1.23.0 ("Checking 40 lines", "Check finished: 7 suggestions - 2 spelling, ...", "Line 4, spelling error: ...").
/// Sheet and model texts are only ever inserted as plain text.
/// </summary>
public static class AssistText
{
    private static readonly (SuggestionCategoryDto Category, Func<CategoryCountsDto, int> Count)[] Counted =
    [
        (SuggestionCategoryDto.Spelling, c => c.Spelling),
        (SuggestionCategoryDto.Grammar, c => c.Grammar),
        (SuggestionCategoryDto.Flow, c => c.Flow),
        (SuggestionCategoryDto.Licence, c => c.Licence),
        (SuggestionCategoryDto.Device, c => c.Device),
        (SuggestionCategoryDto.Alternative, c => c.Alternative),
        (SuggestionCategoryDto.Continuation, c => c.Continuation),
        (SuggestionCategoryDto.Title, c => c.Title),
    ];

    /// <summary>The contract (wire) name of an enum value, e.g. <c>spelling</c>, <c>device.internal-rhyme</c>.</summary>
    /// <typeparam name="T">The enum.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The wire name.</returns>
    public static string Wire<T>(T value)
        where T : struct, Enum => JsonSerializer.Serialize(value).Trim('"');

    /// <summary>The readable category name.</summary>
    /// <param name="category">The category.</param>
    /// <returns>The name.</returns>
    public static string Category(SuggestionCategoryDto category) => category == SuggestionCategoryDto.Licence ? "poetic licence" : Wire(category);

    /// <summary>"Poetic licence: elision", "Device: internal rhyme", "Spelling: typo".</summary>
    /// <param name="type">The type.</param>
    /// <returns>The label.</returns>
    public static string Type(SuggestionTypeDto type)
    {
        var parts = Wire(type).Split('.', 2);
        var group = parts[0] switch
        {
            "licence" => "Poetic licence",
            "write" => "Writing help",
            var other => char.ToUpperInvariant(other[0]) + other[1..],
        };
        return $"{group}: {parts[1].Replace('-', ' ')}";
    }

    /// <summary>"11 -> 9 syllables", or null when the suggestion has no counts.</summary>
    /// <param name="s">The suggestion.</param>
    /// <returns>The text.</returns>
    public static string? Syllables(LyricSuggestionDto s) =>
        s is { SyllablesBefore: { } before, SyllablesAfter: { } after } ? $"{Number(before)} -> {Number(after)} syllables" : null;

    /// <summary>The announcement when an assist starts ("Checking 40 lines").</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="lines">The lines of the sheet.</param>
    /// <returns>The text.</returns>
    public static string Started(AssistKindDto kind, int lines) => kind switch
    {
        AssistKindDto.Check => $"Checking {Lines(lines)}",
        AssistKindDto.Devices => $"Looking for devices in {Lines(lines)}",
        AssistKindDto.Alternatives => "Writing alternatives",
        AssistKindDto.Continue => "Writing a continuation",
        _ => "Writing title ideas",
    };

    /// <summary>"Check finished: 7 suggestions - 2 spelling, 1 grammar, 3 flow, 1 device".</summary>
    /// <param name="kind">The kind.</param>
    /// <param name="counts">The counts of the completed event.</param>
    /// <returns>The text.</returns>
    public static string Finished(AssistKindDto kind, CategoryCountsDto counts)
    {
        var parts = Counted.Select(c => (c.Category, Count: c.Count(counts))).Where(c => c.Count > 0).ToList();
        var total = parts.Sum(p => p.Count);
        var head = $"{KindName(kind)} finished: {Number(total)} {(total == 1 ? "suggestion" : "suggestions")}";
        return parts.Count == 0 ? head : head + " - " + string.Join(", ", parts.Select(p => $"{Number(p.Count)} {Category(p.Category)}"));
    }

    /// <summary>"Line 4, spelling error: wiet, proposed weet. Explanation".</summary>
    /// <param name="s">The focused suggestion.</param>
    /// <returns>The text.</returns>
    public static string Focused(LyricSuggestionDto s)
    {
        var where = s.Anchor.Line == 0 ? "Title" : $"Line {Number(s.Anchor.Line)}";
        var proposal = s.Proposal is null ? string.Empty : $", proposed {s.Proposal}";
        return $"{where}, {Category(s.Category)} {Wire(s.Severity)}: {s.Anchor.Original}{proposal}. {s.Explanation}";
    }

    /// <summary>The name of an assist kind in sentences ("Check", "Devices", ...).</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The name.</returns>
    public static string KindName(AssistKindDto kind) => kind switch
    {
        AssistKindDto.Check => "Check",
        AssistKindDto.Devices => "Device suggestions",
        AssistKindDto.Alternatives => "Alternatives",
        AssistKindDto.Continue => "Continuation",
        _ => "Title ideas",
    };

    /// <summary>"n suggestions withheld (too close to existing lyrics)".</summary>
    /// <param name="count">The count.</param>
    /// <returns>The text.</returns>
    public static string Withheld(int count) =>
        $"{Number(count)} {(count == 1 ? "suggestion" : "suggestions")} withheld (too close to existing lyrics)";

    /// <summary>An invariant number.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Lines(int lines) => $"{Number(lines)} {(lines == 1 ? "line" : "lines")}";
}
