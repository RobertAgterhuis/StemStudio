using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.TestSupport;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Shared lookups of the skeleton contract tests (features 005 + 007, 006 + 008, 009 + 010, 011 Phase 0, the 011 follow-up claude-key, 012, 013, 022, 023, 014, 015, 016 and 020).</summary>
internal static class ContractJson
{
    private static readonly Lazy<OpenApiContract> LazyWorker =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend007 = new(() => Backend("007"));

    private static readonly Lazy<OpenApiContract> LazyBackend006 = new(() => Backend("006"));

    private static readonly Lazy<OpenApiContract> LazyBackend008 = new(() => Backend("008"));

    private static readonly Lazy<OpenApiContract> LazyBackend009 = new(() => Backend("009"));

    private static readonly Lazy<OpenApiContract> LazyBackend010 = new(() => Backend("010"));

    private static readonly Lazy<OpenApiContract> LazyBackend011 = new(() => Backend("011"));

    private static readonly Lazy<OpenApiContract> LazyBackend1210 = new(() => Backend("011-claude-key"));

    private static readonly Lazy<OpenApiContract> LazyBackend012 = new(() => Backend("012"));

    private static readonly Lazy<OpenApiContract> LazyBackend013 = new(() => Backend("013"));

    private static readonly Lazy<OpenApiContract> LazyWorker013 =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "worker-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend014Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "014", "practice-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend023 = new(() => Backend("023"));

    private static readonly Lazy<OpenApiContract> LazyBackend013Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "backend-api.delta.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend022Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "022", "backend-api.library-folders.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend022 = new(() => Backend("022"));

    private static readonly Lazy<OpenApiContract> LazyBackend014 = new(() => Backend("014"));

    private static readonly Lazy<OpenApiContract> LazyBackend015Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "015", "recording-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend015 = new(() => Backend("015"));

    private static readonly Lazy<OpenApiContract> LazyWorker014 =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "014", "worker-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend016Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "016", "backend-api.delta.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend016 = new(() => Backend("016"));

    private static readonly Lazy<OpenApiContract> LazyWorker016 =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "016", "worker-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyBackend020Delta =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "020", "backend-api.delta.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyGenerator =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "generator-api.openapi.yaml")));

    private static readonly Lazy<OpenApiContract> LazyGenerator009 =
        new(() => OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "009", "generator-api.openapi.yaml")));

    private static readonly string[] TimeMembers = ["createdAt", "updatedAt", "completedAt", "consentedAt"];

    /// <summary>The 007 backend document (1.5.0), linked as <c>Contract/007/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend007 => LazyBackend007.Value;

    /// <summary>The 006 backend document (1.6.0), linked as <c>Contract/006/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend006 => LazyBackend006.Value;

    /// <summary>The 008 backend document (1.7.0), linked as <c>Contract/008/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend008 => LazyBackend008.Value;

    /// <summary>The 009 backend document (1.8.0), linked as <c>Contract/009/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend009 => LazyBackend009.Value;

    /// <summary>The 010 backend document (1.9.0), linked as <c>Contract/010/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend010 => LazyBackend010.Value;

    /// <summary>The 011 backend document (1.10.0), linked as <c>Contract/011/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend011 => LazyBackend011.Value;

    /// <summary>The 011 follow-up claude-key backend document (1.21.0), linked as <c>Contract/011-claude-key/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend1210 => LazyBackend1210.Value;

    /// <summary>The 012 backend document (1.11.0), linked as <c>Contract/012/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend012 => LazyBackend012.Value;

    /// <summary>The 013 backend document (1.12.0), linked as <c>Contract/013/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend013 => LazyBackend013.Value;

    /// <summary>The standalone 013 delta (1.12.0-delta.1), linked as <c>Contract/013/backend-api.delta.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend013Delta => LazyBackend013Delta.Value;

    /// <summary>The standalone 022 delta (1.22.0-delta.1), linked as <c>Contract/022/backend-api.library-folders.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend022Delta => LazyBackend022Delta.Value;

    /// <summary>The 022 backend document (1.22.0 = 013's 1.12.0 + 022), linked as <c>Contract/022/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend022 => LazyBackend022.Value;

    /// <summary>The 023 backend document (1.23.0 = 022's 1.22.0 + 023), linked as <c>Contract/023/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend023 => LazyBackend023.Value;

    /// <summary>The 013 worker document (1.7.0), linked as <c>Contract/013/worker-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Worker013 => LazyWorker013.Value;

    /// <summary>The standalone 014 backend delta (1.13.0-delta.2), linked as <c>Contract/014/practice-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend014Delta => LazyBackend014Delta.Value;

    /// <summary>The 014 backend document (1.13.0 = 023's 1.23.0 + 014), linked as <c>Contract/014/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend014 => LazyBackend014.Value;

    /// <summary>The standalone 015 delta (1.14.0-delta.2), linked as <c>Contract/015/recording-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend015Delta => LazyBackend015Delta.Value;

    /// <summary>The 015 backend document (1.14.0 = 014's 1.13.0 + 015), linked as <c>Contract/015/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend015 => LazyBackend015.Value;

    /// <summary>The 014 worker document (1.8.0), linked as <c>Contract/014/worker-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Worker014 => LazyWorker014.Value;

    /// <summary>The standalone 016 backend delta (1.15.0-delta.2), linked as <c>Contract/016/backend-api.delta.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend016Delta => LazyBackend016Delta.Value;

    /// <summary>The 016 backend document (1.15.0), linked as <c>Contract/016/backend-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend016 => LazyBackend016.Value;

    /// <summary>The 016 worker document (1.9.0), linked as <c>Contract/016/worker-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Worker016 => LazyWorker016.Value;

    /// <summary>The standalone 020 backend delta (1.19.0-delta.2), linked as <c>Contract/020/backend-api.delta.openapi.yaml</c>.</summary>
    public static OpenApiContract Backend020Delta => LazyBackend020Delta.Value;

    /// <summary>The generator document the code follows (1.1.0, feature 010).</summary>
    public static OpenApiContract GeneratorContract => LazyGenerator.Value;

    /// <summary>The 009 generator document (1.0.0), linked as <c>Contract/009/generator-api.openapi.yaml</c>.</summary>
    public static OpenApiContract Generator009 => LazyGenerator009.Value;

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

    /// <summary>A copy without any date-time member (at every depth); see <see cref="WithoutTimes"/>.</summary>
    public static JsonNode WithoutAnyTimes(JsonNode node)
    {
        var copy = node.DeepClone();
        Strip(copy);
        return copy;
    }

    /// <summary>Serializes <c>T</c> parsed from the backend example back; date-time members removed at every depth.</summary>
    public static (JsonNode Expected, JsonNode Actual) RoundTripDeep<T>(string example)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize<T>(Web), Web)!;
        return (WithoutAnyTimes(json), WithoutAnyTimes(again));
    }

    /// <summary>A generator 1.1.0 example value.</summary>
    public static JsonNode GeneratorExample(string name) => GeneratorContract.Root["components"]!["examples"]![name]!["value"]!;

    /// <summary>True when <c>Generator/Examples/&lt;file&gt;.json</c> equals the contract JSON.</summary>
    public static bool IsGeneratorCopyOf(string file, JsonNode? expected) =>
        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Generator", "Examples", file + ".json"))), expected);

    /// <summary>Asserts that every enum's wire names equal the <c>enum</c> of its backend schema, in order.</summary>
    public static void AssertEnumsMatch(IEnumerable<(Type Enum, string Schema)> pairs)
    {
        foreach (var (type, schema) in pairs)
        {
            WireNames(type).ShouldBe(Strings(Schema(schema)["enum"]!), schema);
        }
    }

    private static void Strip(JsonNode? node)
    {
        var children = node switch
        {
            JsonObject obj => StripTimes(obj),
            JsonArray array => [.. array],
            _ => [],
        };
        foreach (var child in children)
        {
            Strip(child);
        }
    }

    private static List<JsonNode?> StripTimes(JsonObject obj)
    {
        foreach (var name in TimeMembers)
        {
            obj.Remove(name);
        }

        return [.. obj.Select(pair => pair.Value)];
    }
    public static JsonNode Schema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    public static JsonNode Example(string name) => OpenApiContract.Backend.Root["components"]!["examples"]![name]!["value"]!.DeepClone();

    public static JsonNode WorkerExample(string name) => WorkerContract.Root["components"]!["examples"]![name]!["value"]!;

    private static OpenApiContract Backend(string feature) =>
        OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", feature, "backend-api.openapi.yaml"));

    public static IEnumerable<string> Strings(JsonNode array) => array.AsArray().Select(v => v!.GetValue<string>());

    /// <summary>The <c>operationId</c> of an operation.</summary>
    public static string OperationId(JsonObject operation) => operation["operationId"]!.GetValue<string>();

    /// <summary>"method path operationId" of an operation, for comparing operation sets.</summary>
    public static string Signature((string Path, string Method, JsonObject Operation) op) => $"{op.Method} {op.Path} {OperationId(op.Operation)}";

    /// <summary>The entry names of a document section.</summary>
    public static IEnumerable<string> Keys(OpenApiContract contract, string section) => Section(contract.Root, section).Select(entry => entry.Key);

    /// <summary>The values of the string constants of a class.</summary>
    public static IEnumerable<string> ConstantValues(Type type) =>
        type.GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

    /// <summary>The wire names of every member of a contract enum, in declaration order.</summary>
    public static IEnumerable<string> WireNames(Type enumType) =>
        Enum.GetValues(enumType).Cast<object>().Select(v => JsonSerializer.SerializeToNode(v, enumType, Web)!.GetValue<string>());

    /// <summary>True when <c>Worker/Examples/&lt;file&gt;.json</c> equals the contract JSON.</summary>
    public static bool IsCopyOf(string file, JsonNode? expected) => JsonNode.DeepEquals(JsonNode.Parse(WorkerExamples.Read(file)), expected);
}
