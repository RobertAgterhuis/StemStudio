using System.Text.Json;
using System.Text.RegularExpressions;

namespace StemStudio.Web.Tests.Skeleton;

/// <summary>
/// Dependency-free validator for the JSON Schema 2020-12 keyword subset used by the 004 help
/// schemas (contracts/help-content.md 4.1). JsonSchema.Net is excluded (OSMF EULA, not OSI), so the
/// tests use this subset; any keyword outside it throws <see cref="NotSupportedException"/> so a
/// schema change can never be silently ignored.
/// </summary>
internal sealed class SchemaSubsetValidator
{
    private delegate void Keyword(SchemaSubsetValidator validator, Context context);

    private static readonly HashSet<string> Annotations = ["$schema", "$id", "title", "description", "$defs"];

    private static readonly Dictionary<string, Keyword> ValueKeywords = new(StringComparer.Ordinal)
    {
        ["type"] = (_, c) => CheckType(c),
        ["enum"] = (_, c) => CheckEnum(c),
        ["pattern"] = (_, c) => CheckPattern(c),
        ["minLength"] = (_, c) => CheckLength(c, min: true),
        ["maxLength"] = (_, c) => CheckLength(c, min: false),
        ["minimum"] = (_, c) => CheckMinimum(c),
    };

    private static readonly Dictionary<string, Keyword> StructureKeywords = new(StringComparer.Ordinal)
    {
        ["required"] = (_, c) => CheckRequired(c),
        ["additionalProperties"] = (_, c) => CheckAdditional(c),
        ["properties"] = (v, c) => v.CheckProperties(c),
        ["items"] = (v, c) => v.CheckItems(c),
        ["minItems"] = (_, c) => CheckCount(c, min: true),
        ["maxItems"] = (_, c) => CheckCount(c, min: false),
        ["uniqueItems"] = (_, c) => CheckUnique(c),
        ["$ref"] = (v, c) => v.CheckRef(c),
    };

    private readonly JsonElement _root;

    private SchemaSubsetValidator(JsonElement root) => _root = root;

    /// <summary>Loads a schema file.</summary>
    public static SchemaSubsetValidator Load(string path) =>
        new(JsonDocument.Parse(File.ReadAllText(path)).RootElement.Clone());

    /// <summary>Validates an instance; returns one message per violation (empty when valid).</summary>
    public IReadOnlyList<string> Validate(JsonElement instance)
    {
        var errors = new List<string>();
        Check(_root, instance, "$", errors);
        return errors;
    }

    private void Check(JsonElement schema, JsonElement instance, string path, List<string> errors)
    {
        foreach (var keyword in schema.EnumerateObject())
        {
            if (Annotations.Contains(keyword.Name))
            {
                continue;
            }

            if (!ValueKeywords.TryGetValue(keyword.Name, out var check) && !StructureKeywords.TryGetValue(keyword.Name, out check))
            {
                throw new NotSupportedException($"Schema keyword '{keyword.Name}' is not supported by the subset validator.");
            }

            check(this, new Context(schema, keyword.Value, instance, path, errors));
        }
    }

    private static void CheckType(Context c)
    {
        var allowed = c.Value.ValueKind == JsonValueKind.Array
            ? c.Value.EnumerateArray().Select(t => t.GetString()).ToList()
            : [c.Value.GetString()];
        var actual = TypeOf(c.Instance);
        var ok = allowed.Contains(actual) || (actual == "integer" && allowed.Contains("number"));
        c.FailIf(!ok, $"type {actual} is not {string.Join("|", allowed)}");
    }

    private static string TypeOf(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => "object",
        JsonValueKind.Array => "array",
        JsonValueKind.String => "string",
        JsonValueKind.Number => e.TryGetInt64(out _) ? "integer" : "number",
        JsonValueKind.True or JsonValueKind.False => "boolean",
        _ => "null",
    };

    private static void CheckEnum(Context c) =>
        c.FailIf(!c.Value.EnumerateArray().Any(v => JsonElement.DeepEquals(v, c.Instance)), $"{c.Instance} is not an allowed value");

    private static void CheckPattern(Context c)
    {
        if (c.Instance.ValueKind == JsonValueKind.String)
        {
            var text = c.Instance.GetString()!;
            c.FailIf(!Regex.IsMatch(text, c.Value.GetString()!, RegexOptions.None, TimeSpan.FromSeconds(1)), $"'{text}' does not match {c.Value}");
        }
    }

    private static void CheckLength(Context c, bool min)
    {
        if (c.Instance.ValueKind == JsonValueKind.String)
        {
            var length = c.Instance.GetString()!.Length;
            c.FailIf(min ? length < c.Value.GetInt32() : length > c.Value.GetInt32(), $"length {length} violates {(min ? "minLength" : "maxLength")} {c.Value}");
        }
    }

    private static void CheckMinimum(Context c) =>
        c.FailIf(c.Instance.ValueKind == JsonValueKind.Number && c.Instance.GetDouble() < c.Value.GetDouble(), $"{c.Instance} is below minimum {c.Value}");

    private static void CheckRequired(Context c)
    {
        if (c.Instance.ValueKind == JsonValueKind.Object)
        {
            foreach (var name in c.Value.EnumerateArray().Select(n => n.GetString()!))
            {
                c.FailIf(!c.Instance.TryGetProperty(name, out _), $"required property '{name}' is missing");
            }
        }
    }

    private static void CheckAdditional(Context c)
    {
        if (c.Value.ValueKind != JsonValueKind.False)
        {
            throw new NotSupportedException("Only 'additionalProperties: false' is supported.");
        }

        var declared = c.Schema.TryGetProperty("properties", out var props) ? props : default;
        foreach (var property in ObjectProperties(c.Instance))
        {
            c.FailIf(declared.ValueKind != JsonValueKind.Object || !declared.TryGetProperty(property.Name, out _), $"property '{property.Name}' is not allowed");
        }
    }

    private void CheckProperties(Context c)
    {
        foreach (var property in ObjectProperties(c.Instance))
        {
            if (c.Value.TryGetProperty(property.Name, out var sub))
            {
                Check(sub, property.Value, $"{c.Path}.{property.Name}", c.Errors);
            }
        }
    }

    private void CheckItems(Context c)
    {
        var index = 0;
        foreach (var item in ArrayItems(c.Instance))
        {
            Check(c.Value, item, $"{c.Path}[{index++}]", c.Errors);
        }
    }

    private static void CheckCount(Context c, bool min)
    {
        if (c.Instance.ValueKind == JsonValueKind.Array)
        {
            var count = c.Instance.GetArrayLength();
            c.FailIf(min ? count < c.Value.GetInt32() : count > c.Value.GetInt32(), $"{count} items violate {(min ? "minItems" : "maxItems")} {c.Value}");
        }
    }

    private static void CheckUnique(Context c)
    {
        var items = ArrayItems(c.Instance).Select(i => i.GetRawText()).ToList();
        c.FailIf(c.Value.ValueKind == JsonValueKind.True && items.Distinct(StringComparer.Ordinal).Count() != items.Count, "items are not unique");
    }

    private void CheckRef(Context c)
    {
        const string prefix = "#/$defs/";
        var reference = c.Value.GetString()!;
        if (!reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            throw new NotSupportedException($"Only local '{prefix}' references are supported, got '{reference}'.");
        }

        Check(_root.GetProperty("$defs").GetProperty(reference[prefix.Length..]), c.Instance, c.Path, c.Errors);
    }

    private static List<JsonProperty> ObjectProperties(JsonElement e) =>
        e.ValueKind == JsonValueKind.Object ? [.. e.EnumerateObject()] : [];

    private static List<JsonElement> ArrayItems(JsonElement e) =>
        e.ValueKind == JsonValueKind.Array ? [.. e.EnumerateArray()] : [];

    private readonly record struct Context(JsonElement Schema, JsonElement Value, JsonElement Instance, string Path, List<string> Errors)
    {
        public void FailIf(bool failed, string message)
        {
            if (failed)
            {
                Errors.Add($"{Path}: {message}");
            }
        }
    }
}
