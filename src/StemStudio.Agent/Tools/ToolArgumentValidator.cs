using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using StemStudio.Contracts.Agent;

namespace StemStudio.Agent.Tools;

/// <summary>Outcome of validating one call's arguments.</summary>
/// <param name="IsValid">True when the arguments match the schema.</param>
/// <param name="Arguments">The parsed arguments (an object; empty when invalid).</param>
/// <param name="Canonical">Canonical JSON of the arguments.</param>
/// <param name="Hash">SHA-256 of <paramref name="Canonical"/>.</param>
/// <param name="Error">Readable error for the model when invalid.</param>
public sealed record ArgumentValidation(bool IsValid, JsonElement Arguments, string Canonical, string Hash, string? Error);

/// <summary>
/// Validates arguments against a <see cref="ToolSchema"/> (types, ranges, enums, patterns, uuid, closed objects,
/// cross-argument rules, 8 KB limit) before any policy decision (FR-008).
/// </summary>
public static class ToolArgumentValidator
{
    private static readonly TimeSpan PatternTimeout = TimeSpan.FromMilliseconds(100);
    private static readonly JsonElement EmptyObject = JsonDocument.Parse("{}").RootElement.Clone();

    /// <summary>Validates the JSON arguments of a call.</summary>
    /// <param name="schema">The tool's schema.</param>
    /// <param name="json">Arguments as sent by the model; null, blank or "null" mean no arguments.</param>
    /// <returns>The validation.</returns>
    public static ArgumentValidation Validate(ToolSchema schema, string? json)
    {
        ArgumentNullException.ThrowIfNull(schema);
        if (!TryParseObject(json, out var arguments))
        {
            return Invalid(EmptyObject, "Arguments must be a JSON object.");
        }

        var canonical = CanonicalJson.Canonicalize(arguments);
        return Encoding.UTF8.GetByteCount(canonical) > AgentContractLimits.ArgumentsMaxBytes
            ? Invalid(arguments, "Arguments exceed the 8 KB limit.")
            : Checked(schema, arguments, canonical);
    }

    private static ArgumentValidation Checked(ToolSchema schema, JsonElement arguments, string canonical)
    {
        var errors = new List<string>();
        CheckObject(schema.Parameters, arguments, string.Empty, errors);
        CheckRules(schema.Rules, arguments, errors);
        return new ArgumentValidation(errors.Count == 0, arguments, canonical, CanonicalJson.Hash(canonical), errors.Count == 0 ? null : string.Join("; ", errors));
    }

    private static ArgumentValidation Invalid(JsonElement arguments, string error)
    {
        var canonical = CanonicalJson.Canonicalize(arguments);
        return new ArgumentValidation(false, arguments, canonical, CanonicalJson.Hash(canonical), error);
    }

    private static bool TryParseObject(string? json, out JsonElement arguments)
    {
        arguments = EmptyObject;
        return string.IsNullOrWhiteSpace(json) || json.Trim() == "null" || TryParse(json, out arguments);
    }

    private static bool TryParse(string json, out JsonElement arguments)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            arguments = document.RootElement.Clone();
            return arguments.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            arguments = EmptyObject;
            return false;
        }
    }

    private static void CheckObject(IReadOnlyList<ToolParameter> parameters, JsonElement value, string path, List<string> errors)
    {
        foreach (var property in value.EnumerateObject().Where(property => !parameters.Any(parameter => parameter.Name == property.Name)))
        {
            errors.Add($"Unknown argument '{path}{property.Name}'.");
        }

        foreach (var parameter in parameters)
        {
            CheckProperty(parameter, value, path, errors);
        }
    }

    private static void CheckProperty(ToolParameter parameter, JsonElement value, string path, List<string> errors)
    {
        var name = path + parameter.Name;
        if (value.TryGetProperty(parameter.Name, out var item) && item.ValueKind != JsonValueKind.Null)
        {
            CheckValue(parameter, item, name, errors);
        }
        else if (parameter.Required)
        {
            errors.Add($"Argument '{name}' is required.");
        }
    }

    private static void CheckValue(ToolParameter parameter, JsonElement value, string name, List<string> errors)
    {
        var error = parameter.Type switch
        {
            ParameterType.Text => CheckText(parameter, value, name),
            ParameterType.WholeNumber or ParameterType.Number => CheckNumber(parameter, value, name),
            ParameterType.Flag => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : $"Argument '{name}' must be true or false.",
            ParameterType.List => CheckList(parameter, value, name, errors),
            _ => CheckRecord(parameter, value, name, errors),
        };
        if (error is not null)
        {
            errors.Add(error);
        }
    }

    private static string? CheckText(ToolParameter parameter, JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.String)
        {
            return $"Argument '{name}' must be a string.";
        }

        var text = value.GetString()!;
        return LengthError(parameter, text, name) ?? EnumError(parameter, text, name) ?? FormatError(parameter, text, name);
    }

    private static string? LengthError(ToolParameter parameter, string text, string name) =>
        text.Length < (parameter.MinLength ?? 0) || text.Length > (parameter.MaxLength ?? int.MaxValue)
            ? $"Argument '{name}' must have {parameter.MinLength ?? 0} to {parameter.MaxLength ?? int.MaxValue} characters."
            : null;

    private static string? EnumError(ToolParameter parameter, string text, string name) =>
        parameter.Enum is { } values && !values.Contains(text, StringComparer.Ordinal)
            ? $"Argument '{name}' must be one of: {string.Join(", ", values)}."
            : null;

    private static string? FormatError(ToolParameter parameter, string text, string name)
    {
        if (parameter.Format == ToolParameter.UuidFormat && !Guid.TryParse(text, out _))
        {
            return $"Argument '{name}' must be a UUID (use the ids from earlier tool results).";
        }

        return parameter.Pattern is { } pattern && !Regex.IsMatch(text, pattern, RegexOptions.CultureInvariant, PatternTimeout)
            ? $"Argument '{name}' must match {pattern}."
            : null;
    }

    private static string? CheckNumber(ToolParameter parameter, JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            return $"Argument '{name}' must be a number.";
        }

        var number = value.GetDouble();
        return parameter.Type == ParameterType.WholeNumber && number % 1 != 0
            ? $"Argument '{name}' must be a whole number."
            : RangeError(parameter, number, name);
    }

    private static string? RangeError(ToolParameter parameter, double number, string name) =>
        number < (parameter.Minimum ?? double.MinValue) || number > (parameter.Maximum ?? double.MaxValue)
            ? $"Argument '{name}' must be between {Format(parameter.Minimum)} and {Format(parameter.Maximum)}."
            : null;

    private static string Format(double? number) => number?.ToString(CultureInfo.InvariantCulture) ?? "any";

    private static string? CheckList(ToolParameter parameter, JsonElement value, string name, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            return $"Argument '{name}' must be an array.";
        }

        if (CountError(parameter, value.GetArrayLength(), name) is { } error)
        {
            return error;
        }

        CheckItems(parameter.Items!, value, name, errors);
        return null;
    }

    private static string? CountError(ToolParameter parameter, int count, string name) =>
        count < (parameter.MinItems ?? 0) || count > (parameter.MaxItems ?? int.MaxValue)
            ? $"Argument '{name}' must have {parameter.MinItems ?? 0} to {parameter.MaxItems ?? int.MaxValue} items."
            : null;

    private static void CheckItems(ToolParameter item, JsonElement list, string name, List<string> errors)
    {
        var index = 0;
        foreach (var element in list.EnumerateArray())
        {
            CheckValue(item, element, $"{name}[{index++}]", errors);
        }
    }

    private static string? CheckRecord(ToolParameter parameter, JsonElement value, string name, List<string> errors)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            return $"Argument '{name}' must be an object.";
        }

        CheckObject(parameter.Properties, value, name + ".", errors);
        return null;
    }

    private static void CheckRules(IReadOnlyList<SchemaRule> rules, JsonElement arguments, List<string> errors)
    {
        foreach (var rule in rules)
        {
            if (RuleError(rule, Present(rule, arguments)) is { } error)
            {
                errors.Add(error);
            }
        }
    }

    private static int Present(SchemaRule rule, JsonElement arguments) =>
        rule.Names.Count(name => arguments.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null);

    private static string? RuleError(SchemaRule rule, int present) =>
        rule.Kind == SchemaRuleKind.AtMostOne && present > 1 ? $"Use only one of {string.Join(", ", rule.Names)}."
        : rule.Kind == SchemaRuleKind.AtLeastOne && present == 0 ? $"One of {string.Join(", ", rule.Names)} is required."
        : null;
}
