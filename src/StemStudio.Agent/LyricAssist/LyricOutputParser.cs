using System.Text.Json;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.LyricAssist;

/// <summary>The schema-checked content of one block's model output.</summary>
/// <param name="Items">Items that match <c>$defs/item</c> of lyric-assist-output 1.0.0.</param>
/// <param name="Misses">Miss verdicts that match the schema.</param>
/// <param name="InvalidItems">Items and misses dropped because they do not match the schema.</param>
public sealed record ParsedLyricOutput(IReadOnlyList<ModelSuggestionItem> Items, IReadOnlyList<ModelMissVerdict> Misses, int InvalidItems);

/// <summary>
/// Parses the untrusted model output against lyric-assist-output 1.0.0 (pure, research R-05). The whole output is invalid
/// (null: the block is retried once, then skipped) when it is not a JSON object with an <c>items</c> array, has other root keys or
/// more than 40 items or misses; a single item or miss that breaks the schema is dropped and counted.
/// </summary>
public static class LyricOutputParser
{
    private const int MaxEntries = 40;
    private const int MaxWord = 40;
    private static readonly HashSet<string> RootKeys = new(StringComparer.Ordinal) { "items", "misses" };
    private static readonly HashSet<string> ItemKeys = new(StringComparer.Ordinal) { "type", "severity", "line", "original", "proposal", "explanation", "rhymeWithLine", "rhymeWord" };
    private static readonly HashSet<string> MissKeys = new(StringComparer.Ordinal) { "line", "word", "verdict", "correction", "licenceType" };
    private static readonly HashSet<string> LicenceTypes = new(StringComparer.Ordinal) { "elision", "inversion", "archaism", "sound-spelling", "slang", "code-switch" };

    /// <summary>Parses one output.</summary>
    /// <param name="json">Raw model text.</param>
    /// <returns>The parsed output, or null when the output as a whole is invalid.</returns>
    public static ParsedLyricOutput? Parse(string? json)
    {
        if (TryRoot(json) is not { } root)
        {
            return null;
        }

        using (root)
        {
            var items = root.RootElement.GetProperty("items").EnumerateArray().Select(Item).ToList();
            var misses = root.RootElement.TryGetProperty("misses", out var list) && list.ValueKind == JsonValueKind.Array
                ? list.EnumerateArray().Select(Miss).ToList()
                : [];
            var invalid = items.Count(item => item is null) + misses.Count(miss => miss is null);
            return new ParsedLyricOutput([.. items.OfType<ModelSuggestionItem>()], [.. misses.OfType<ModelMissVerdict>()], invalid);
        }
    }

    private static JsonDocument? TryRoot(string? json)
    {
        JsonDocument? document = null;
        try
        {
            document = JsonDocument.Parse(json ?? string.Empty);
            if (ValidRoot(document.RootElement))
            {
                return document;
            }
        }
        catch (JsonException)
        {
            // Not JSON: the whole output is invalid.
        }

        document?.Dispose();
        return null;
    }

    private static bool ValidRoot(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.EnumerateObject().All(property => RootKeys.Contains(property.Name))
        && root.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array && items.GetArrayLength() <= MaxEntries
        && (!root.TryGetProperty("misses", out var misses) || (misses.ValueKind == JsonValueKind.Array && misses.GetArrayLength() <= MaxEntries));

    private static ModelSuggestionItem? Item(JsonElement item)
    {
        if (!Closed(item, ItemKeys) || Text(item, "type") is not { } typeName || LyricTypes.Parse(typeName) is not { } type
            || Severity(Text(item, "severity")) is not { } severity || Whole(item, "line", 0) is not { } line
            || Text(item, "original", 0, LyricAssistLimits.ProposalMaxLength) is not { } original
            || Text(item, "explanation", 1, LyricAssistLimits.ExplanationMaxLength) is not { } explanation)
        {
            return null;
        }

        return Optionals(item, new ModelSuggestionItem { Type = type, Severity = severity, Line = line, Original = original, Explanation = explanation });
    }

    private static ModelSuggestionItem? Optionals(JsonElement item, ModelSuggestionItem parsed)
    {
        var proposal = OptionalText(item, "proposal", LyricAssistLimits.ProposalMaxLength);
        var rhymeLine = OptionalWhole(item, "rhymeWithLine", 1);
        var rhymeWord = OptionalText(item, "rhymeWord", MaxWord);
        return proposal.Valid && rhymeLine.Valid && rhymeWord.Valid
            ? parsed with { Proposal = proposal.Value, RhymeWithLine = rhymeLine.Value, RhymeWord = rhymeWord.Value }
            : null;
    }

    private static ModelMissVerdict? Miss(JsonElement miss)
    {
        if (!Closed(miss, MissKeys) || Whole(miss, "line", 1) is not { } line || Text(miss, "word", 1, MaxWord) is not { } word
            || Verdict(Text(miss, "verdict")) is not { } verdict)
        {
            return null;
        }

        var correction = OptionalText(miss, "correction", MaxWord);
        var licence = OptionalText(miss, "licenceType", MaxWord);
        return correction.Valid && licence.Valid && (licence.Value is null || LicenceTypes.Contains(licence.Value))
            ? new ModelMissVerdict { Line = line, Word = word, Verdict = verdict, Correction = correction.Value, LicenceType = licence.Value }
            : null;
    }

    private static bool Closed(JsonElement value, HashSet<string> keys) =>
        value.ValueKind == JsonValueKind.Object && value.EnumerateObject().All(property => keys.Contains(property.Name));

    private static string? Text(JsonElement value, string name, int min = 1, int max = int.MaxValue) =>
        value.TryGetProperty(name, out var text) && text.ValueKind == JsonValueKind.String && text.GetString()!.Length is var length && length >= min && length <= max
            ? text.GetString()
            : null;

    private static int? Whole(JsonElement value, string name, int min) =>
        value.TryGetProperty(name, out var number) && number.ValueKind == JsonValueKind.Number && number.TryGetInt32(out var whole) && whole >= min ? whole : null;

    private static (bool Valid, string? Value) OptionalText(JsonElement value, string name, int max) =>
        !value.TryGetProperty(name, out var text) || text.ValueKind == JsonValueKind.Null ? (true, null)
        : text.ValueKind == JsonValueKind.String && text.GetString()!.Length <= max ? (true, text.GetString())
        : (false, null);

    private static (bool Valid, int? Value) OptionalWhole(JsonElement value, string name, int min) =>
        !value.TryGetProperty(name, out var number) || number.ValueKind == JsonValueKind.Null ? (true, null)
        : Whole(value, name, min) is { } whole ? (true, whole)
        : (false, null);

    private static SuggestionSeverityDto? Severity(string? name) => name switch
    {
        "error" => SuggestionSeverityDto.Error,
        "improvement" => SuggestionSeverityDto.Improvement,
        "idea" => SuggestionSeverityDto.Idea,
        _ => null,
    };

    private static MissVerdict? Verdict(string? name) => name switch
    {
        "typo" => MissVerdict.Typo,
        "licence" => MissVerdict.Licence,
        "keep" => MissVerdict.Keep,
        _ => null,
    };
}
