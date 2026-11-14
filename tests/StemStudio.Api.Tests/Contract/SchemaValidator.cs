using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Validates JSON against the OpenAPI 3.1 schema subset used by backend-api.openapi.yaml: $ref, allOf, oneOf, type
/// (incl. type arrays with null), enum, const, required, properties, additionalProperties, items, min/maxItems,
/// min/maxLength, pattern, minimum, maximum, exclusiveMinimum and the formats uuid and date-time.
/// </summary>
internal sealed class SchemaValidator(OpenApiContract contract)
{
    private readonly List<string> _errors = [];

    public static IReadOnlyList<string> Validate(OpenApiContract contract, JsonNode schema, JsonNode? instance)
    {
        var validator = new SchemaValidator(contract);
        validator.Check(schema, instance, "$");
        return validator._errors;
    }

    private void Check(JsonNode schemaNode, JsonNode? instance, string path)
    {
        var schema = contract.Resolve(schemaNode).AsObject();
        if (!CheckComposition(schema, instance, path) || !CheckType(schema, instance, path) || instance is null)
        {
            return;
        }

        CheckEnumAndConst(schema, instance, path);
        switch (instance)
        {
            case JsonObject obj:
                CheckObject(schema, obj, path);
                break;
            case JsonArray array:
                CheckArray(schema, array, path);
                break;
            default:
                CheckScalar(schema, instance.AsValue(), path);
                break;
        }
    }

    private bool CheckComposition(JsonObject schema, JsonNode? instance, string path)
    {
        foreach (var part in schema["allOf"]?.AsArray() ?? [])
        {
            Check(part!, instance, path);
        }

        if (schema["oneOf"] is not JsonArray options)
        {
            return true;
        }

        var matches = options.Count(option => Validate(contract, option!, instance).Count == 0);
        Expect(matches == 1, path, $"matches {matches} oneOf alternatives");
        return false;
    }

    private bool CheckType(JsonObject schema, JsonNode? instance, string path)
    {
        if (schema["type"] is not { } typeNode)
        {
            return true;
        }

        var types = typeNode is JsonArray list ? list.Select(t => t?.GetValue<string>() ?? "null").ToList() : [typeNode.GetValue<string>()];
        var actual = TypeOf(instance);
        var ok = types.Contains(actual) || (actual == "integer" && types.Contains("number"));
        Expect(ok, path, $"is {actual}, expected {string.Join("|", types)}");
        return ok;
    }

    private void CheckEnumAndConst(JsonObject schema, JsonNode instance, string path)
    {
        if (schema["enum"] is JsonArray values)
        {
            Expect(values.Any(value => JsonNode.DeepEquals(Normalize(value), Normalize(instance))), path, $"value {instance.ToJsonString()} not in enum");
        }

        if (schema["const"] is { } constant)
        {
            Expect(JsonNode.DeepEquals(Normalize(constant), Normalize(instance)), path, $"value {instance.ToJsonString()} is not {constant.ToJsonString()}");
        }
    }

    private void CheckObject(JsonObject schema, JsonObject obj, string path)
    {
        foreach (var required in schema["required"]?.AsArray() ?? [])
        {
            Expect(obj.ContainsKey(required!.GetValue<string>()), path, $"misses required property '{required}'");
        }

        var properties = schema["properties"]?.AsObject();
        foreach (var (name, value) in obj)
        {
            CheckProperty(schema, properties, (name, value), path);
        }
    }

    private void CheckProperty(JsonObject schema, JsonObject? properties, (string Name, JsonNode? Value) property, string path)
    {
        var childPath = $"{path}.{property.Name}";
        if (properties?[property.Name] is { } propertySchema)
        {
            Check(propertySchema, property.Value, childPath);
            return;
        }

        switch (schema["additionalProperties"])
        {
            case JsonValue allowed when allowed.GetValue<bool>() is false:
                _errors.Add($"{childPath}: additional property not allowed");
                break;
            case JsonObject additional:
                Check(additional, property.Value, childPath);
                break;
        }
    }

    private void CheckArray(JsonObject schema, JsonArray array, string path)
    {
        Expect(array.Count >= (Number(schema, "minItems") ?? 0), path, "has too few items");
        Expect(array.Count <= (Number(schema, "maxItems") ?? double.MaxValue), path, "has too many items");
        for (var i = 0; i < array.Count && schema["items"] is { } items; i++)
        {
            Check(items, array[i], $"{path}[{i}]");
        }
    }

    private void CheckScalar(JsonObject schema, JsonValue value, string path)
    {
        if (value.GetValueKind() == JsonValueKind.String)
        {
            CheckString(schema, value.GetValue<string>(), path);
        }
        else if (value.GetValueKind() == JsonValueKind.Number)
        {
            CheckNumber(schema, AsDouble(value), path);
        }
    }

    private void CheckString(JsonObject schema, string text, string path)
    {
        Expect(text.Length >= (Number(schema, "minLength") ?? 0), path, "is too short");
        Expect(text.Length <= (Number(schema, "maxLength") ?? double.MaxValue), path, "is too long");
        if (schema["pattern"] is { } pattern)
        {
            Expect(Regex.IsMatch(text, pattern.GetValue<string>(), RegexOptions.None, TimeSpan.FromSeconds(1)), path, $"'{text}' does not match {pattern}");
        }

        CheckFormat(schema["format"]?.GetValue<string>(), text, path);
    }

    private void CheckFormat(string? format, string text, string path)
    {
        var ok = format switch
        {
            "uuid" => Guid.TryParseExact(text, "D", out _),
            "date-time" => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out _) && text.Contains('T', StringComparison.Ordinal),
            _ => true,
        };
        Expect(ok, path, $"'{text}' is not a valid {format}");
    }

    private void CheckNumber(JsonObject schema, double number, string path)
    {
        Expect(number >= (Number(schema, "minimum") ?? double.MinValue), path, $"{number} is below the minimum");
        Expect(number <= (Number(schema, "maximum") ?? double.MaxValue), path, $"{number} is above the maximum");
        Expect(number > (Number(schema, "exclusiveMinimum") ?? double.MinValue), path, $"{number} is not above the exclusive minimum");
    }

    private void Expect(bool condition, string path, string message)
    {
        if (!condition)
        {
            _errors.Add($"{path}: {message}");
        }
    }

    private static double? Number(JsonObject schema, string keyword) => (schema[keyword] is { } limit ? AsDouble(limit) : null);

    private static JsonNode? Normalize(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.Number ? JsonValue.Create(AsDouble(value)) : node;

    private static string TypeOf(JsonNode? node) => node switch
    {
        null => "null",
        JsonObject => "object",
        JsonArray => "array",
        _ => node.GetValueKind() switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.True or JsonValueKind.False => "boolean",
            _ => IsInteger(AsDouble(node)) ? "integer" : "number",
        },
    };

    private static bool IsInteger(double value) => Math.Abs(value % 1) < double.Epsilon;

    private static double AsDouble(JsonNode node) => double.Parse(node.ToJsonString(), CultureInfo.InvariantCulture);
}
