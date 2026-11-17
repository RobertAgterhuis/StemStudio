using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.Extensions.AI;

namespace StemStudio.Agent.Backends.Fakes;

/// <summary>
/// Result placeholders of the fake agent script (CTO decision for 023 Q4, qa T060): a call argument whose whole value is
/// <c>{{result.&lt;tool&gt;.ids:&lt;category&gt;}}</c> becomes a JSON array of the ids of the items with that category in the most
/// recent result of <c>&lt;tool&gt;</c> earlier in the same run. Supported list tools and their id field are in
/// <see cref="IdFields"/> (only <c>list_lyric_suggestions</c> today). Any other text is passed through unchanged.
/// </summary>
public static partial class ScriptResultPlaceholders
{
    /// <summary>The list tools a result placeholder may name, with the id field of their items.</summary>
    public static IReadOnlyDictionary<string, string> IdFields { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["list_lyric_suggestions"] = "suggestionId",
    };

    /// <summary>True when the text is a whole result placeholder.</summary>
    /// <param name="text">An argument value.</param>
    /// <returns>True for <c>{{result.&lt;tool&gt;.ids:&lt;category&gt;}}</c>.</returns>
    public static bool IsPlaceholder(string text) => Pattern().IsMatch(text);

    /// <summary>Replaces every whole result placeholder in the arguments (also inside lists and records).</summary>
    /// <param name="arguments">The scripted arguments.</param>
    /// <param name="messages">The messages of the current model request (earlier calls and results of the run).</param>
    /// <returns>The arguments with the placeholders resolved.</returns>
    /// <exception cref="InvalidOperationException">The tool is not a supported list tool, or the run has no earlier call of it.</exception>
    public static JsonElement? Resolve(JsonElement? arguments, IReadOnlyList<ChatMessage> messages)
    {
        if (arguments is not { } value || !value.GetRawText().Contains("{{result.", StringComparison.Ordinal))
        {
            return arguments;
        }

        var resolved = Replace(JsonNode.Parse(value.GetRawText()), messages);
        return JsonSerializer.SerializeToElement(resolved);
    }

    private static JsonNode? Replace(JsonNode? node, IReadOnlyList<ChatMessage> messages) => node switch
    {
        JsonObject record => new JsonObject([.. record.Select(property => KeyValuePair.Create(property.Key, Replace(property.Value, messages)))]),
        JsonArray list => new JsonArray([.. list.Select(item => Replace(item, messages))]),
        JsonValue text when text.TryGetValue<string>(out var value) && Pattern().Match(value) is { Success: true } match =>
            Ids(match.Groups["tool"].Value, match.Groups["category"].Value, messages),
        _ => node?.DeepClone(),
    };

    private static JsonArray Ids(string tool, string category, IReadOnlyList<ChatMessage> messages)
    {
        if (!IdFields.TryGetValue(tool, out var idField))
        {
            throw new InvalidOperationException($"The agent script names {{{{result.{tool}...}}}}, but {tool} is not a supported list tool.");
        }

        var result = LatestResult(tool, messages)
            ?? throw new InvalidOperationException($"The agent script uses {{{{result.{tool}.ids:{category}}}}} before any {tool} call in the run.");
        return new JsonArray([.. Items(result)
            .Where(item => item["category"] is JsonValue kind && kind.TryGetValue<string>(out var name) && name == category)
            .Select(item => item[idField]?.DeepClone())
            .Where(id => id is not null)]);
    }

    private static string? LatestResult(string tool, IReadOnlyList<ChatMessage> messages)
    {
        var contents = messages.SelectMany(message => message.Contents).ToList();
        var callIds = contents.OfType<FunctionCallContent>().Where(call => call.Name == tool).Select(call => call.CallId).ToHashSet(StringComparer.Ordinal);
        return contents.OfType<FunctionResultContent>().LastOrDefault(result => callIds.Contains(result.CallId))?.Result?.ToString();
    }

    private static IEnumerable<JsonObject> Items(string result)
    {
        try
        {
            return JsonNode.Parse(result) is JsonObject { } root && root["items"] is JsonArray items ? items.OfType<JsonObject>() : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    [GeneratedRegex(@"\A\{\{result\.(?<tool>[a-z_]+)\.ids:(?<category>[a-z-]+)\}\}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern();
}
