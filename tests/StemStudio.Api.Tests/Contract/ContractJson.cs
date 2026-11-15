using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Shared lookups of the skeleton contract tests (features 005 + 007 Phase 0).</summary>
internal static class ContractJson
{
    private static readonly Lazy<OpenApiContract> LazyWorker =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml")));

    /// <summary>The serializer options of the API (camelCase, string enums by attribute).</summary>
    public static JsonSerializerOptions Web { get; } = new(JsonSerializerDefaults.Web);

    /// <summary>worker-api.openapi.yaml as linked into the test output.</summary>
    public static OpenApiContract WorkerContract => LazyWorker.Value;

    public static JsonNode Schema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    public static JsonNode Example(string name) => OpenApiContract.Backend.Root["components"]!["examples"]![name]!["value"]!.DeepClone();

    public static JsonNode WorkerExample(string name) => WorkerContract.Root["components"]!["examples"]![name]!["value"]!;

    public static IEnumerable<string> Strings(JsonNode array) => array.AsArray().Select(v => v!.GetValue<string>());

    /// <summary>The wire names of every member of a contract enum, in declaration order.</summary>
    public static IEnumerable<string> WireNames(Type enumType) =>
        Enum.GetValues(enumType).Cast<object>().Select(v => JsonSerializer.SerializeToNode(v, enumType, Web)!.GetValue<string>());

    /// <summary>True when <c>Worker/Examples/&lt;file&gt;.json</c> equals the contract JSON.</summary>
    public static bool IsCopyOf(string file, JsonNode? expected) => JsonNode.DeepEquals(JsonNode.Parse(WorkerExamples.Read(file)), expected);
}
