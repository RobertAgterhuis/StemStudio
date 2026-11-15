using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace StemStudio.Web.Tests.Skeleton;

/// <summary>
/// Converts the YAML front matter of a help topic to JSON so it can be validated against
/// <c>help-topic.schema.json</c> (contracts/help-content.md 2). Plain <c>null</c>/<c>~</c> scalars
/// become JSON null, plain integers become numbers, everything else stays a string.
/// </summary>
internal static class FrontMatterJson
{
    private const string Fence = "---";

    /// <summary>Extracts the front matter block of a topic file and returns it as a JSON element.</summary>
    public static JsonElement FromTopic(string markdown)
    {
        var lines = markdown.ReplaceLineEndings("\n").Split('\n');
        if (lines.Length == 0 || lines[0] != Fence)
        {
            throw new FormatException("A topic must start with a '---' front matter fence.");
        }

        var end = Array.IndexOf(lines, Fence, 1);
        if (end < 0)
        {
            throw new FormatException("The front matter has no closing '---' fence.");
        }

        return FromYaml(string.Join('\n', lines[1..end]));
    }

    /// <summary>Parses a YAML mapping and returns it as a JSON element.</summary>
    public static JsonElement FromYaml(string yaml)
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(yaml));
        return JsonSerializer.SerializeToElement(ToNode(stream.Documents[0].RootNode));
    }

    private static JsonNode? ToNode(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(p =>
            KeyValuePair.Create(((YamlScalarNode)p.Key).Value!, ToNode(p.Value)))),
        YamlSequenceNode seq => new JsonArray([.. seq.Children.Select(ToNode)]),
        YamlScalarNode scalar => ToScalar(scalar),
        _ => throw new FormatException($"Unsupported YAML node {node.NodeType}."),
    };

    private static JsonValue? ToScalar(YamlScalarNode scalar)
    {
        if (scalar.Style != ScalarStyle.Plain)
        {
            return JsonValue.Create(scalar.Value);
        }

        if (scalar.Value is null or "null" or "~")
        {
            return null;
        }

        return long.TryParse(scalar.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? JsonValue.Create(number)
            : JsonValue.Create(scalar.Value);
    }
}
