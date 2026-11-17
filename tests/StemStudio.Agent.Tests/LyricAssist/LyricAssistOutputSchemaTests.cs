using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Agent.LyricAssist;
using StemStudio.Contracts.LyricAssist;

namespace StemStudio.Agent.Tests.LyricAssist;

/// <summary>
/// 023 Phase 0 (T002): the embedded model output schema is the contract file lyric-assist-output 1.0.0 and agrees with the backend
/// contract enums (meta-schema validation and sample validation: <c>contracts/check_output_schema.py</c>).
/// </summary>
public sealed class LyricAssistOutputSchemaTests
{
    private static readonly string ContractPath = Path.Combine(AppContext.BaseDirectory, "Contract", "023", "lyric-assist-output.schema.json");

    private static JsonNode Item => LyricAssistOutputSchema.Parse()["$defs"]!["item"]!;

    [Fact]
    public void Embedded_IsTheContractFile()
    {
        LyricAssistOutputSchema.Text.ShouldBe(File.ReadAllText(ContractPath));
        LyricAssistOutputSchema.Parse()["$id"]!.GetValue<string>().ShouldEndWith("/lyric-assist-output/" + LyricAssistOutputSchema.Version);
    }

    [Fact]
    public void Root_IsClosedAndNeedsItems()
    {
        var root = LyricAssistOutputSchema.Parse();

        root["$schema"]!.GetValue<string>().ShouldBe("https://json-schema.org/draft/2020-12/schema");
        root["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
        Strings(root["required"]!).ShouldBe(["items"]);
        Item["additionalProperties"]!.GetValue<bool>().ShouldBeFalse();
    }

    [Fact]
    public void ItemTypes_AreTheContractTypesTheModelMayProduce()
    {
        var wire = Enum.GetValues<SuggestionTypeDto>().Select(Wire).Where(type => type != "spelling.unknown-word");

        Strings(Item["properties"]!["type"]!["enum"]!).ShouldBe(wire);
    }

    [Fact]
    public void ItemSeverities_AreTheContractSeverities()
    {
        Strings(Item["properties"]!["severity"]!["enum"]!).ShouldBe(Enum.GetValues<SuggestionSeverityDto>().Select(Wire));
    }

    [Fact]
    public void ItemLimits_MatchTheBackendLimits()
    {
        var properties = Item["properties"]!;

        Max(properties["original"]!, "maxLength").ShouldBe(LyricAssistLimits.ProposalMaxLength);
        Max(properties["proposal"]!, "maxLength").ShouldBe(LyricAssistLimits.ProposalMaxLength);
        Max(properties["explanation"]!, "maxLength").ShouldBe(LyricAssistLimits.ExplanationMaxLength);
        Max(properties["rhymeWord"]!, "maxLength").ShouldBe(LyricAssistLimits.WordMaxLength);
    }

    [Fact]
    public void MissVerdicts_MatchTheEnum()
    {
        var verdict = LyricAssistOutputSchema.Parse()["properties"]!["misses"]!["items"]!["properties"]!["verdict"]!;

        Strings(verdict["enum"]!).ShouldBe(Enum.GetValues<MissVerdict>().Select(v => v.ToString().ToLowerInvariant()));
    }

    [Fact]
    public void Parse_ReturnsAFreshCopy()
    {
        var first = LyricAssistOutputSchema.Parse();
        first.Remove("properties");

        LyricAssistOutputSchema.Parse().ContainsKey("properties").ShouldBeTrue();
    }

    private static string Wire<T>(T value)
        where T : struct, Enum => JsonSerializer.Serialize(value).Trim('"');

    private static int Max(JsonNode node, string keyword) => node[keyword]!.GetValue<int>();

    private static IEnumerable<string> Strings(JsonNode array) => array.AsArray().Select(v => v!.GetValue<string>());
}
