using System.Text.Json;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>The closed type list of the suggestions (data-model 4): contract names, categories and the types each kind may produce.</summary>
public static class LyricTypes
{
    private static readonly Dictionary<string, SuggestionTypeDto> ByName =
        Enum.GetValues<SuggestionTypeDto>().ToDictionary(Name, type => type, StringComparer.Ordinal);

    /// <summary>The default categories of <c>check</c> (devices are a separate kind).</summary>
    public static IReadOnlyList<SuggestionCategoryDto> CheckCategories { get; } =
        [SuggestionCategoryDto.Spelling, SuggestionCategoryDto.Grammar, SuggestionCategoryDto.Flow, SuggestionCategoryDto.Licence];

    /// <summary>The categories of <c>devices</c> (FR-013: devices and poetic licence).</summary>
    public static IReadOnlyList<SuggestionCategoryDto> DeviceCategories { get; } = [SuggestionCategoryDto.Device, SuggestionCategoryDto.Licence];

    /// <summary>The contract name of a type, e.g. <c>spelling.typo</c>.</summary>
    /// <param name="type">The type.</param>
    /// <returns>The name.</returns>
    public static string Name(SuggestionTypeDto type) => JsonSerializer.Serialize(type).Trim('"');

    /// <summary>The type of a contract name, or null for an unknown name.</summary>
    /// <param name="name">A name from the model output.</param>
    /// <returns>The type.</returns>
    public static SuggestionTypeDto? Parse(string? name) => name is not null && ByName.TryGetValue(name, out var type) ? type : null;

    /// <summary>The category of a type (prefix before the dot; <c>write.*</c> by kind of writing help).</summary>
    /// <param name="type">The type.</param>
    /// <returns>The category.</returns>
    public static SuggestionCategoryDto Category(SuggestionTypeDto type) => type switch
    {
        SuggestionTypeDto.WriteAlternative => SuggestionCategoryDto.Alternative,
        SuggestionTypeDto.WriteContinuation => SuggestionCategoryDto.Continuation,
        SuggestionTypeDto.WriteTitle => SuggestionCategoryDto.Title,
        _ => Enum.Parse<SuggestionCategoryDto>(Name(type).Split('.')[0], ignoreCase: true),
    };

    /// <summary>The categories an assist of the kind may produce, narrowed to the requested ones (empty = the kind's defaults).</summary>
    /// <param name="kind">Assist kind.</param>
    /// <param name="requested">Requested categories of <c>check</c> / <c>devices</c>.</param>
    /// <returns>The allowed categories.</returns>
    public static IReadOnlyList<SuggestionCategoryDto> Allowed(AssistKindDto kind, IReadOnlyList<SuggestionCategoryDto> requested)
    {
        IReadOnlyList<SuggestionCategoryDto> defaults = kind switch
        {
            AssistKindDto.Check => CheckCategories,
            AssistKindDto.Devices => DeviceCategories,
            AssistKindDto.Alternatives => [SuggestionCategoryDto.Alternative],
            AssistKindDto.Continue => [SuggestionCategoryDto.Continuation],
            _ => [SuggestionCategoryDto.Title],
        };
        var narrowed = defaults.Where(requested.Contains).ToList();
        return requested.Count == 0 || narrowed.Count == 0 ? defaults : narrowed;
    }

    /// <summary>True for a correction (spelling, grammar, flow): it needs a proposal and may change at most 60 % of the line.</summary>
    /// <param name="category">The category.</param>
    /// <returns>True for corrections.</returns>
    public static bool IsCorrection(SuggestionCategoryDto category) =>
        category is SuggestionCategoryDto.Spelling or SuggestionCategoryDto.Grammar or SuggestionCategoryDto.Flow;

    /// <summary>True for the writing kinds (alternatives, continue, titles): temperature 0.8.</summary>
    /// <param name="kind">Assist kind.</param>
    /// <returns>True for writing help.</returns>
    public static bool IsWriting(AssistKindDto kind) => kind is AssistKindDto.Alternatives or AssistKindDto.Continue or AssistKindDto.Titles;
}
