using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Shared lookups of the skeleton contract tests (features 005 + 007 and 006 + 008 Phase 0).</summary>
internal static class ContractJson
{
    private static readonly Lazy<OpenApiContract> LazyWorker =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend007 = new(() => Backend("007"));

    private static readonly Lazy<OpenApiContract> LazyBackend006 = new(() => Backend("006"));

    /// <summary>The 007 backend document (1.5.0), linked as <c>Contract/007/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend007 => LazyBackend007.Value;

    /// <summary>The 006 backend document (1.6.0), linked as <c>Contract/006/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend006 => LazyBackend006.Value;

    /// <summary>The serializer options of the API (camelCase, string enums by attribute).</summary>
    public static JsonSerializerOptions Web { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>worker-api.openapi.yaml as linked into the test output.</summary>
    public static OpenApiContract WorkerContract => LazyWorker.Value;

    /// <summary>A section such as <c>paths</c> or <c>components/schemas</c> of a document.</summary>
    public static JsonObject Section(JsonObject root, string section) =>
        section.Split('/').Aggregate((JsonNode)root, (node, part) => node[part]!).AsObject();

    /// <summary>Keys of <paramref name="older"/>'s section whose value differs in <paramref name="newer"/>.</summary>
    public static IEnumerable<string> ChangedEntries(OpenApiContract older, OpenApiContract newer, string section)
    {
        var after = Section(newer.Root, section);
        return Section(older.Root, section).Where(entry => !JsonNode.DeepEquals(entry.Value, after[entry.Key])).Select(entry => entry.Key);
    }

    /// <summary>The document sections compared by the subset tests.</summary>
    public static TheoryData<string> Sections => new("paths", "components/schemas", "components/responses", "components/examples", "components/parameters", "components/headers");

    /// <summary>A copy without <c>createdAt</c>/<c>updatedAt</c>: DateTimeOffset writes "Z" as "+00:00" (same instant); the schema check covers the format.</summary>
    public static JsonObject WithoutTimes(JsonNode node)
    {
        var copy = node.DeepClone().AsObject();
        copy.Remove("createdAt");
        copy.Remove("updatedAt");
        return copy;
    }

    /// <summary>Serializes <c>T</c> parsed from the backend example back and returns both (times removed when asked).</summary>
    public static (JsonNode Expected, JsonNode Actual) RoundTrip<T>(string example, bool ignoreTimes = false)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize<T>(Web), Web)!;
        return ignoreTimes ? (WithoutTimes(json), WithoutTimes(again)) : (json, again);
    }

    public static JsonNode Schema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    public static JsonNode Example(string name) => OpenApiContract.Backend.Root["components"]!["examples"]![name]!["value"]!.DeepClone();

    public static JsonNode WorkerExample(string name) => WorkerContract.Root["components"]!["examples"]![name]!["value"]!;

    private static OpenApiContract Backend(string feature) =>
        OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", feature, "backend-api.openapi.yaml"));

    public static IEnumerable<string> Strings(JsonNode array) => array.AsArray().Select(v => v!.GetValue<string>());

    /// <summary>The wire names of every member of a contract enum, in declaration order.</summary>
    public static IEnumerable<string> WireNames(Type enumType) =>
        Enum.GetValues(enumType).Cast<object>().Select(v => JsonSerializer.SerializeToNode(v, enumType, Web)!.GetValue<string>());

    /// <summary>True when <c>Worker/Examples/&lt;file&gt;.json</c> equals the contract JSON.</summary>
    public static bool IsCopyOf(string file, JsonNode? expected) => JsonNode.DeepEquals(JsonNode.Parse(WorkerExamples.Read(file)), expected);
}
