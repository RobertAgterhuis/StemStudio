using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.TestDoubles.ClaudeKey;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 011 follow-up claude-key Phase 0, T002/T004): backend-api 1.21.0
/// (<c>specs/011-ai-composer-agent/followups/claude-key/contracts/backend-api.openapi.yaml</c>) is 011's 1.10.0 plus exactly
/// the standalone delta <c>backend-api.claude-key.openapi.yaml</c> (1.21.0-delta.2, DELTA.md section 1), and the Claude key
/// DTOs, enums, constants and routes match it.
/// </summary>
public sealed class ClaudeKeyContractTests
{
    private static readonly Lazy<OpenApiContract> Delta = new(() =>
        OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "011-claude-key", "backend-api.claude-key.openapi.yaml")));

    private static readonly DateTimeOffset StoredAt = new(2026, 11, 16, 15, 40, 0, TimeSpan.Zero);

    // Base components the delta copies to validate on its own; the merged document keeps their 1.10.0 definitions.
    private static readonly string[] SharedWith1100 =
        ["ValidationProblem", "ProblemDetails", "ValidationProblemDetails", "AgentBackend", "ClaudeModel", "OllamaModelInfo", "OllamaStatus", "AgentStatus"];

    private static readonly string[] KeyOperations = ["getClaudeKeyStatus", "storeClaudeKey", "deleteClaudeKey"];

    // DELTA.md section 1, rules 3 and 4: the only 1.10.0 entries 1.21.0 replaces; /api/agent/settings changes only the text of
    // updateAgentSettings (pre-release text change at integration, T053; pinned by SettingsPath_DiffersOnlyInTheUpdateDescription).
    public static TheoryData<string, string[]> Replaced1100Entries => new()
    {
        { "paths", ["/api/agent/status", "/api/agent/settings/test", ApiRoutes.AgentSettings] },
        { "components/schemas", ["ClaudeStatus", "TestAgentBackendRequest", "BackendTestResult"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string> DeltaSections => new("paths", "components/responses", "components/schemas");

    [Fact]
    public void Delta_IsTheAdditive1210DraftWithOnlyAgentPaths()
    {
        Delta.Value.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.21.0-delta.2");
        Delta.Value.Operations.ShouldAllBe(op => op.Path.StartsWith(ApiRoutes.Agent + "/", StringComparison.Ordinal));
        OperationIds(Delta.Value).ShouldBe(["getAgentStatus", .. KeyOperations, "testAgentBackend"], ignoreOrder: true);
    }

    [Fact]
    public void Backend1210_Is1100PlusExactlyTheThreeKeyOperations()
    {
        var added = Delta.Value.Operations.Where(op => KeyOperations.Contains(OperationId(op.Operation)));

        Backend1210.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.21.0");
        Backend1210.Operations.Select(Signature).ShouldBe(Backend011.Operations.Concat(added).Select(Signature), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Replaced1100Entries))]
    public void Backend1210_Contains1100UnchangedExceptTheReplacedAgentEntries(string section, string[] replaced)
    {
        ChangedEntries(Backend011, Backend1210, section).ShouldBe(replaced, ignoreOrder: true);
    }

    [Fact]
    public void SettingsPath_DiffersFrom1100OnlyInTheUpdateDescription()
    {
        var before = Section(Backend011.Root, "paths")[ApiRoutes.AgentSettings]!.DeepClone();
        var after = Section(Backend1210.Root, "paths")[ApiRoutes.AgentSettings]!.DeepClone();
        before["put"]!.AsObject().Remove("description");
        after["put"]!.AsObject().Remove("description");

        JsonNode.DeepEquals(before, after).ShouldBeTrue();
    }

    [Fact]
    public void UpdateAgentSettings_NeedsAUsableKeyFromEitherSource()
    {
        var description = Section(Backend1210.Root, "paths")[ApiRoutes.AgentSettings]!["put"]!["description"]!.GetValue<string>();

        description.ShouldContain("a usable Claude key");
        description.ShouldContain("stored in Settings");
        description.ShouldNotContain("needs `ANTHROPIC_API_KEY`");
    }

    [Fact]
    public void KeyStoreUnavailableExample_IsTheApiText()
    {
        foreach (var document in new[] { Backend1210, Delta.Value })
        {
            var example = document.Root["components"]!["responses"]!["KeyStoreUnavailable"]!["content"]!["application/problem+json"]!["example"]!;

            example["detail"]!.GetValue<string>().ShouldBe(StemStudio.Agent.Backends.ClaudeKeyProvider.StoreUnavailableReason(StemStudio.Agent.Model.KeyStoreUnavailableReasons.NotWritable));
        }
    }

    [Theory]
    [MemberData(nameof(DeltaSections))]
    public void Backend1210_ContainsTheDeltaUnchangedExceptTheShared1100Components(string section)
    {
        var merged = Section(Backend1210.Root, section);

        ChangedEntries(Delta.Value, Backend1210, section).ShouldBeSubsetOf(SharedWith1100);
        Section(Delta.Value.Root, section).Select(entry => entry.Key).Where(SharedWith1100.Contains)
            .ShouldAllBe(key => JsonNode.DeepEquals(Section(Backend011.Root, section)[key], merged[key]));
    }

    [Theory]
    [MemberData(nameof(DeltaSections))]
    public void Backend1210_AddsNothingButTheDeltaEntries(string section)
    {
        var expected = Keys(Backend011, section).Union(Keys(Delta.Value, section));

        Keys(Backend1210, section).ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [InlineData(typeof(ClaudeKeySourceDto), "ClaudeKeySource")]
    [InlineData(typeof(ClaudeKeyStateDto), "ClaudeKeyState")]
    [InlineData(typeof(StoredClaudeKeyStateDto), "StoredClaudeKeyState")]
    [InlineData(typeof(ClaudeKeyTestSourceDto), "ClaudeKeyTestSource")]
    public void Enums_MatchTheContract(Type enumType, string schema)
    {
        WireNames(enumType).ShouldBe(Strings(Schema(schema)["enum"]!));
    }

    [Fact]
    public void InvalidReasons_MatchTheProblemSchema()
    {
        var reason = Schema("ClaudeKeyInvalidProblem")["allOf"]![1]!["properties"]!["reason"]!;

        Strings(reason["enum"]!).ShouldBe(ConstantValues(typeof(ClaudeKeyInvalidReasons)));
    }

    [Theory]
    [InlineData("InvalidBody", AgentProblemTypes.InvalidBody, 400)]
    [InlineData("LocalOnly", AgentProblemTypes.LocalOnly, 403)]
    [InlineData("ClaudeKeyInvalid", AgentProblemTypes.ClaudeKeyInvalid, 422)]
    [InlineData("KeyStoreUnavailable", AgentProblemTypes.KeyStoreUnavailable, 503)]
    public void ProblemTypes_MatchTheResponseExamples(string response, string code, int status)
    {
        var example = Backend1210.Root["components"]!["responses"]![response]!["content"]!["application/problem+json"]!["example"]!;

        (example["type"]!.GetValue<string>(), (int)example["status"]!.GetValue<double>()).ShouldBe((AgentProblemTypes.Uri(code), status));
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var apiKey = Schema("StoreClaudeKeyRequest")["properties"]!["apiKey"]!;

        ((int)apiKey["maxLength"]!.GetValue<double>()).ShouldBe(AgentContractLimits.ClaudeKeyRequestMaxLength);
        AgentContractLimits.ClaudeKeyBodyBytes.ShouldBe(1024);
        Backend1210.Response("storeClaudeKey", 413).ShouldNotBeNull();
    }

    [Fact]
    public void Route_IsTheKeyResourceWithItsThreeOperations()
    {
        var path = Section(Backend1210.Root, "paths")[ApiRoutes.AgentClaudeKey]!.AsObject();

        path.Select(method => OperationId(method.Value!.AsObject())).ShouldBe(KeyOperations);
        ApiRoutes.AgentClaudeKey.ShouldBe("/api/agent/settings/claude-key");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void StatusExamples_RoundTrip(int index)
    {
        var example = Schema("ClaudeKeyStatus")["examples"]![index]!.DeepClone().AsObject();

        var dto = example.Deserialize<ClaudeKeyStatusDto>(Web)!;

        dto.StoredAt.ShouldBe(DateTimeOffset.Parse(example["storedAt"]!.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture));
        JsonNode.DeepEquals(Without(JsonSerializer.SerializeToNode(dto, Web)!, "storedAt"), Without(example, "storedAt")).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Dtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var json = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(Backend1210, Schema(schema), json).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Dtos_WriteExactlyTheSchemaProperties(string schema, object dto)
    {
        var written = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)!.AsObject().Select(p => p.Key);

        written.ShouldBe(Schema(schema)["properties"]!.AsObject().Select(p => p.Key), ignoreOrder: true);
    }

    [Fact]
    public void OptionalMembers_AreOmittedWhenNull()
    {
        var request = JsonSerializer.SerializeToNode(new TestAgentBackendRequest { Backend = AgentBackendDto.Claude }, Web)!.AsObject();
        var result = JsonSerializer.SerializeToNode(new BackendTestResultDto { Backend = AgentBackendDto.Ollama, Ok = true, LatencyMs = 3, Detail = "Ollama 0.40.16" }, Web)!.AsObject();

        (request.ContainsKey("claudeKeySource"), result.ContainsKey("keySource")).ShouldBe((false, false));
    }

    [Fact]
    public void ClaudeStatus_KeyDefaultsToNotConfigured()
    {
        var status = new ClaudeStatusDto { KeyConfigured = false, NoticeAcknowledged = false, Model = ClaudeModelDto.ClaudeOpus55 };

        status.Key.ShouldBeSameAs(ClaudeKeyStatusDto.NotConfigured);
        (status.Key.State, status.Key.Stored, status.Key.StoreAvailable).ShouldBe((ClaudeKeyStateDto.NotConfigured, StoredClaudeKeyStateDto.Absent, true));
    }

    [Fact]
    public void StoreRequest_ReadsTheValueFromTheWire()
    {
        var json = new JsonObject { ["apiKey"] = ClaudeKeySentinels.Stored }.ToJsonString();

        JsonSerializer.Deserialize<StoreClaudeKeyRequest>(json, Web)!.ApiKey.ShouldBe(ClaudeKeySentinels.Stored);
    }

    [Fact]
    [Trait("Category", "sec")]
    public void StoreRequest_ToStringNeverContainsTheValue()
    {
        var request = new StoreClaudeKeyRequest { ApiKey = ClaudeKeySentinels.Stored };

        var text = $"{request} {string.Join(',', new object[] { request })}";

        ClaudeKeySentinels.SearchForms(ClaudeKeySentinels.Stored).ShouldAllBe(form => !text.Contains(form, StringComparison.Ordinal));
        text.ShouldContain("[redacted]");
    }

    public static TheoryData<string, object> Dtos => new()
    {
        { "ClaudeKeyStatus", ClaudeKeyStatusDto.NotConfigured },
        { "ClaudeKeyStatus", EnvironmentOverStored() },
        { "ClaudeStatus", new ClaudeStatusDto { KeyConfigured = true, NoticeAcknowledged = true, Model = ClaudeModelDto.ClaudeSonnet55, Key = EnvironmentOverStored() } },
        { "StoreClaudeKeyRequest", new StoreClaudeKeyRequest { ApiKey = ClaudeKeySentinels.Stored } },
        { "TestAgentBackendRequest", new TestAgentBackendRequest { Backend = AgentBackendDto.Claude, ClaudeKeySource = ClaudeKeyTestSourceDto.Stored } },
        { "BackendTestResult", new BackendTestResultDto { Backend = AgentBackendDto.Claude, Ok = true, LatencyMs = 180, Detail = "Claude answered", KeySource = ClaudeKeySourceDto.Stored } },
    };

    private static ClaudeKeyStatusDto EnvironmentOverStored() => new()
    {
        Configured = true,
        Source = ClaudeKeySourceDto.Environment,
        State = ClaudeKeyStateDto.Environment,
        LastFour = "ENV1",
        Stored = StoredClaudeKeyStateDto.Readable,
        StoredLastFour = "STO1",
        StoredAt = StoredAt,
        StoreAvailable = true,
        Reason = null,
        Fix = null,
    };

    private static string OperationId(JsonObject operation) => operation["operationId"]!.GetValue<string>();

    private static IEnumerable<string> OperationIds(OpenApiContract contract) => contract.Operations.Select(op => OperationId(op.Operation));

    private static string Signature((string Path, string Method, JsonObject Operation) op) => $"{op.Method} {op.Path} {OperationId(op.Operation)}";

    private static IEnumerable<string> Keys(OpenApiContract contract, string section) => Section(contract.Root, section).Select(entry => entry.Key);

    private static IEnumerable<string> ConstantValues(Type type) =>
        type.GetFields().Where(field => field.IsLiteral).Select(field => (string)field.GetRawConstantValue()!);

    private static JsonObject Without(JsonNode node, string member)
    {
        var copy = node.DeepClone().AsObject();
        copy.Remove(member);
        return copy;
    }
}
