using System.Text.Json;
using System.Text.Json.Nodes;

namespace StemStudio.Agent.Tools;

/// <summary>JSON type of a tool parameter.</summary>
public enum ParameterType
{
    /// <summary>JSON string.</summary>
    Text,

    /// <summary>JSON integer.</summary>
    WholeNumber,

    /// <summary>JSON number.</summary>
    Number,

    /// <summary>JSON boolean.</summary>
    Flag,

    /// <summary>JSON array.</summary>
    List,

    /// <summary>JSON object (closed: additionalProperties false).</summary>
    Record,
}

/// <summary>Cross-argument rule of a schema.</summary>
public enum SchemaRuleKind
{
    /// <summary>At most one of the arguments may be present (e.g. gainDb vs gainChangeDb, research F-11).</summary>
    AtMostOne,

    /// <summary>At least one of the arguments must be present (e.g. trackId or jobId).</summary>
    AtLeastOne,
}

/// <summary>A cross-argument rule.</summary>
/// <param name="Kind">Rule kind.</param>
/// <param name="Names">Argument names.</param>
public sealed record SchemaRule(SchemaRuleKind Kind, IReadOnlyList<string> Names);

/// <summary>One parameter (or array item / nested property) of a tool schema.</summary>
public sealed record ToolParameter
{
    /// <summary>Format value of GUID strings.</summary>
    public const string UuidFormat = "uuid";

    /// <summary>Parameter name (empty for array items).</summary>
    public required string Name { get; init; }

    /// <summary>JSON type.</summary>
    public required ParameterType Type { get; init; }

    /// <summary>Description the model sees.</summary>
    public string? Description { get; init; }

    /// <summary>True when the parameter must be present.</summary>
    public bool Required { get; init; } = true;

    /// <summary>Inclusive minimum of numbers.</summary>
    public double? Minimum { get; init; }

    /// <summary>Inclusive maximum of numbers.</summary>
    public double? Maximum { get; init; }

    /// <summary>Minimum string length.</summary>
    public int? MinLength { get; init; }

    /// <summary>Maximum string length.</summary>
    public int? MaxLength { get; init; }

    /// <summary>Regular expression the string must match.</summary>
    public string? Pattern { get; init; }

    /// <summary>Allowed string values.</summary>
    public IReadOnlyList<string>? Enum { get; init; }

    /// <summary>String format (uuid).</summary>
    public string? Format { get; init; }

    /// <summary>Item schema of arrays.</summary>
    public ToolParameter? Items { get; init; }

    /// <summary>Minimum array length.</summary>
    public int? MinItems { get; init; }

    /// <summary>Maximum array length.</summary>
    public int? MaxItems { get; init; }

    /// <summary>Properties of objects.</summary>
    public IReadOnlyList<ToolParameter> Properties { get; init; } = [];

    /// <summary>True when an explicit JSON <c>null</c> is a meaningful value (e.g. "back to automatic"); the schema type includes null.</summary>
    public bool Nullable { get; init; }

    /// <summary>The same parameter, optional and nullable (omitted keeps, null resets).</summary>
    /// <returns>The parameter.</returns>
    public ToolParameter OptionalOrNull() => this with { Required = false, Nullable = true };

    /// <summary>The same parameter, optional.</summary>
    /// <returns>The parameter.</returns>
    public ToolParameter Optional() => this with { Required = false };

    /// <summary>The same parameter with a description.</summary>
    /// <param name="description">Description.</param>
    /// <returns>The parameter.</returns>
    public ToolParameter Describe(string description) => this with { Description = description };

    /// <summary>The JSON Schema of this parameter.</summary>
    /// <returns>The schema node.</returns>
    public JsonObject ToJsonSchema()
    {
        var schema = new JsonObject { ["type"] = Nullable ? new JsonArray(TypeName(Type), "null") : TypeName(Type) };
        AddTexts(schema);
        AddLimits(schema);
        if (Enum is { } values)
        {
            schema["enum"] = new JsonArray([.. values.Select(value => (JsonNode)JsonValue.Create(value))]);
        }

        if (Items is { } items)
        {
            schema["items"] = items.ToJsonSchema();
        }

        return Type == ParameterType.Record ? ToolSchema.AddProperties(schema, Properties) : schema;
    }

    private void AddTexts(JsonObject schema)
    {
        Add(schema, "description", Description);
        Add(schema, "format", Format);
        Add(schema, "pattern", Pattern);
    }

    private void AddLimits(JsonObject schema)
    {
        AddNumber(schema, "minimum", Minimum);
        AddNumber(schema, "maximum", Maximum);
        AddNumber(schema, "minLength", MinLength);
        AddNumber(schema, "maxLength", MaxLength);
        AddNumber(schema, "minItems", MinItems);
        AddNumber(schema, "maxItems", MaxItems);
    }

    private static string TypeName(ParameterType type) => type switch
    {
        ParameterType.Text => "string",
        ParameterType.WholeNumber => "integer",
        ParameterType.Number => "number",
        ParameterType.Flag => "boolean",
        ParameterType.List => "array",
        _ => "object",
    };

    private static void Add(JsonObject schema, string key, string? value)
    {
        if (value is not null)
        {
            schema[key] = value;
        }
    }

    private static void AddNumber(JsonObject schema, string key, double? value)
    {
        if (value is { } number)
        {
            schema[key] = number % 1 == 0 ? JsonValue.Create((long)number) : JsonValue.Create(number);
        }
    }
}

/// <summary>Builders for tool parameters (tool catalogue argument notation).</summary>
public static class Param
{
    /// <summary>A GUID string.</summary>
    /// <param name="name">Name.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Uuid(string name) => new() { Name = name, Type = ParameterType.Text, Format = ToolParameter.UuidFormat };

    /// <summary>A string with a length range.</summary>
    /// <param name="name">Name.</param>
    /// <param name="min">Minimum length.</param>
    /// <param name="max">Maximum length.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Text(string name, int min, int max) => new() { Name = name, Type = ParameterType.Text, MinLength = min, MaxLength = max };

    /// <summary>A string from a fixed set.</summary>
    /// <param name="name">Name.</param>
    /// <param name="values">Allowed values.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Choice(string name, params IReadOnlyList<string> values) => new() { Name = name, Type = ParameterType.Text, Enum = values };

    /// <summary>A number with a range.</summary>
    /// <param name="name">Name.</param>
    /// <param name="min">Inclusive minimum.</param>
    /// <param name="max">Inclusive maximum.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Number(string name, double min, double max) => new() { Name = name, Type = ParameterType.Number, Minimum = min, Maximum = max };

    /// <summary>An integer with a range.</summary>
    /// <param name="name">Name.</param>
    /// <param name="min">Inclusive minimum.</param>
    /// <param name="max">Inclusive maximum.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Whole(string name, int min, int max) => new() { Name = name, Type = ParameterType.WholeNumber, Minimum = min, Maximum = max };

    /// <summary>A boolean.</summary>
    /// <param name="name">Name.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Flag(string name) => new() { Name = name, Type = ParameterType.Flag };

    /// <summary>An array.</summary>
    /// <param name="name">Name.</param>
    /// <param name="item">Item schema (its name is ignored).</param>
    /// <param name="min">Minimum length.</param>
    /// <param name="max">Maximum length.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter List(string name, ToolParameter item, int min, int max) =>
        new() { Name = name, Type = ParameterType.List, Items = item with { Name = string.Empty }, MinItems = min, MaxItems = max };

    /// <summary>A closed object.</summary>
    /// <param name="name">Name.</param>
    /// <param name="properties">Properties.</param>
    /// <returns>The parameter.</returns>
    public static ToolParameter Record(string name, params IReadOnlyList<ToolParameter> properties) =>
        new() { Name = name, Type = ParameterType.Record, Properties = properties };
}

/// <summary>Argument schema of a tool: closed object of parameters plus cross-argument rules.</summary>
/// <param name="Parameters">Top-level parameters.</param>
public sealed record ToolSchema(IReadOnlyList<ToolParameter> Parameters)
{
    /// <summary>A tool without arguments.</summary>
    public static ToolSchema Empty { get; } = new([]);

    /// <summary>Cross-argument rules.</summary>
    public IReadOnlyList<SchemaRule> Rules { get; init; } = [];

    /// <summary>A schema of parameters.</summary>
    /// <param name="parameters">Parameters.</param>
    /// <returns>The schema.</returns>
    public static ToolSchema Of(params IReadOnlyList<ToolParameter> parameters) => new(parameters);

    /// <summary>The schema with an "at most one of" rule.</summary>
    /// <param name="names">Argument names.</param>
    /// <returns>The schema.</returns>
    public ToolSchema AtMostOne(params IReadOnlyList<string> names) => this with { Rules = [.. Rules, new SchemaRule(SchemaRuleKind.AtMostOne, names)] };

    /// <summary>The schema with an "at least one of" rule.</summary>
    /// <param name="names">Argument names.</param>
    /// <returns>The schema.</returns>
    public ToolSchema AtLeastOne(params IReadOnlyList<string> names) => this with { Rules = [.. Rules, new SchemaRule(SchemaRuleKind.AtLeastOne, names)] };

    /// <summary>The JSON Schema sent to the model (type object, additionalProperties false).</summary>
    /// <returns>The schema.</returns>
    public JsonElement ToJsonSchema() =>
        JsonSerializer.SerializeToElement(AddProperties(new JsonObject { ["type"] = "object" }, Parameters));

    internal static JsonObject AddProperties(JsonObject schema, IReadOnlyList<ToolParameter> properties)
    {
        var map = new JsonObject();
        foreach (var property in properties)
        {
            map[property.Name] = property.ToJsonSchema();
        }

        schema["properties"] = map;
        schema["required"] = new JsonArray([.. properties.Where(property => property.Required).Select(property => (JsonNode)JsonValue.Create(property.Name))]);
        schema["additionalProperties"] = false;
        return schema;
    }
}
