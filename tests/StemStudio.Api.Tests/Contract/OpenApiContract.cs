using System.Globalization;
using System.Text.Json.Nodes;
using YamlDotNet.RepresentationModel;

namespace StemStudio.Api.Tests.Contract;

/// <summary>backend-api.openapi.yaml loaded into a <see cref="JsonNode"/> tree (YamlDotNet + System.Text.Json).</summary>
internal sealed class OpenApiContract
{
    private static readonly string[] Methods = ["get", "put", "post", "delete", "patch"];

    private OpenApiContract(JsonObject root) => Root = root;

    public static OpenApiContract Backend { get; } = Load(Path.Combine(AppContext.BaseDirectory, "Contract", "backend-api.openapi.yaml"));

    public JsonObject Root { get; }

    public IEnumerable<(string Path, string Method, JsonObject Operation)> Operations =>
        Root["paths"]!.AsObject().SelectMany(path => Methods
            .Where(method => path.Value![method] is not null)
            .Select(method => (path.Key, method, path.Value![method]!.AsObject())));

    public static OpenApiContract Load(string path)
    {
        var stream = new YamlStream();
        using (var reader = File.OpenText(path))
        {
            stream.Load(reader);
        }

        return new OpenApiContract(ToJson(stream.Documents[0].RootNode)!.AsObject());
    }

    public JsonObject Operation(string operationId) =>
        Operations.Single(op => op.Operation["operationId"]!.GetValue<string>() == operationId).Operation;

    /// <summary>The response object for a status code, with <c>$ref</c> resolved; null when not declared.</summary>
    public JsonObject? Response(string operationId, int status)
    {
        var responses = Operation(operationId)["responses"]!.AsObject();
        var response = responses[status.ToString(CultureInfo.InvariantCulture)];
        return response is null ? null : Resolve(response).AsObject();
    }

    public JsonNode Resolve(JsonNode node)
    {
        while (node is JsonObject obj && obj["$ref"] is { } reference)
        {
            node = reference.GetValue<string>().TrimStart('#', '/').Split('/').Aggregate((JsonNode)Root, (current, part) => current[part]!);
        }

        return node;
    }

    private static JsonNode? ToJson(YamlNode node) => node switch
    {
        YamlMappingNode map => new JsonObject(map.Children.Select(pair =>
            KeyValuePair.Create(((YamlScalarNode)pair.Key).Value!, ToJson(pair.Value)))),
        YamlSequenceNode sequence => new JsonArray(sequence.Children.Select(ToJson).ToArray()),
        YamlScalarNode scalar => Scalar(scalar),
        _ => throw new NotSupportedException(node.GetType().Name),
    };

    private static JsonValue? Scalar(YamlScalarNode scalar)
    {
        var text = scalar.Value ?? string.Empty;
        if (scalar.Style != YamlDotNet.Core.ScalarStyle.Plain)
        {
            return JsonValue.Create(text);
        }

        return text switch
        {
            "null" or "~" or "" => null,
            "true" => JsonValue.Create(true),
            "false" => JsonValue.Create(false),
            _ when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) => JsonValue.Create(number),
            _ => JsonValue.Create(text),
        };
    }
}
