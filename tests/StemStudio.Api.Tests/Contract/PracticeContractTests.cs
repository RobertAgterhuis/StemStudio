using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts.Mixes;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 012 Phase 0, T003/T004): backend-api 1.11.0
/// (<c>specs/012-practice-tempo/contracts/backend-api.openapi.yaml</c>) is the full 1.21.0 of the 011 follow-up plus exactly the
/// 012 additions (DELTA.md section 1), and the practice DTOs, defaults and limits match it. Since 013 Phase 0 the merge facts are
/// checked on the linked 1.11.0 document (<c>Contract/012</c>); schemas and examples come from the current full document (1.23.0
/// with 013, 022 and 023), which keeps every practice entry unchanged (LyricAssistContractTests, LibraryContractTests).
/// </summary>
public sealed class PracticeContractTests
{
    private static readonly string[] AddedSchemas = ["PracticeSettings", "SpeedTrainerSettings", "CountInSettings"];

    // DELTA.md section 1: the only 1.21.0 entries 1.11.0 changes (MixSettings gains `practice`, PUT /api/mixes/{mixId} an example).
    public static TheoryData<string, string[]> Changed1210Entries => new()
    {
        { "paths", ["/api/mixes/{mixId}"] },
        { "components/schemas", ["MixSettings"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    [Fact]
    public void Backend_IsLabelled1110AndListsTheMergedDeltas()
    {
        var info = Backend012.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.11.0");
        Strings(info["x-stemstudio-contains"]!).ShouldBe(["1.1.0", "1.2.0", "1.3.0", "1.4.0", "1.5.0", "1.6.0", "1.7.0", "1.8.0", "1.9.0", "1.10.0", "1.21.0", "1.11.0"]);
    }

    [Theory]
    [MemberData(nameof(Changed1210Entries))]
    public void Backend1110_Contains1210UnchangedExceptThePracticeEntries(string section, string[] changed)
    {
        ChangedEntries(Backend1210, Backend012, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Fact]
    public void Backend1110_AddsOnlyThePracticeSchemasAndExampleAndNoOperation()
    {
        static IEnumerable<string> Added(string section) =>
            Section(Backend012.Root, section).Select(e => e.Key).Except(Section(Backend1210.Root, section).Select(e => e.Key));

        Added("components/schemas").ShouldBe(AddedSchemas, ignoreOrder: true);
        Added("components/examples").ShouldBe(["MixSettingsWithPractice"]);
        Added("paths").ShouldBeEmpty();
        Backend012.Operations.Count().ShouldBe(Backend1210.Operations.Count());
    }

    [Fact]
    public void MixSettings_GainsOnlyTheOptionalPracticeProperty()
    {
        var before = Schema1210("MixSettings");
        var after = Schema("MixSettings").DeepClone().AsObject();

        after["properties"]!["practice"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/PracticeSettings");
        after["properties"]!.AsObject().Remove("practice");
        JsonNode.DeepEquals(before, after).ShouldBeTrue();
    }

    [Fact]
    public void Defaults_MatchTheSchemaDefaults()
    {
        var practice = Schema("PracticeSettings")["properties"]!;
        var countIn = Schema("CountInSettings")["properties"]!;

        PracticeSettingsDto.Default.ShouldBe(new PracticeSettingsDto(
            Int(practice, "speedPercent"),
            Int(practice, "transposeSemitones"),
            TrainerDefaults(),
            new CountInSettingsDto(Int(countIn, "bars"), Default<bool>(countIn, "everyPass"))));
    }

    [Theory]
    [InlineData("PracticeSettings", "speedPercent", PracticeLimits.MinSpeedPercent, PracticeLimits.MaxSpeedPercent)]
    [InlineData("PracticeSettings", "transposeSemitones", PracticeLimits.MinTransposeSemitones, PracticeLimits.MaxTransposeSemitones)]
    [InlineData("SpeedTrainerSettings", "startPercent", PracticeLimits.MinSpeedPercent, PracticeLimits.MaxTrainerStartPercent)]
    [InlineData("SpeedTrainerSettings", "stepPercent", PracticeLimits.MinTrainerStepPercent, PracticeLimits.MaxTrainerStepPercent)]
    [InlineData("SpeedTrainerSettings", "targetPercent", PracticeLimits.MinTrainerTargetPercent, PracticeLimits.MaxSpeedPercent)]
    [InlineData("SpeedTrainerSettings", "passesPerStep", PracticeLimits.MinPassesPerStep, PracticeLimits.MaxPassesPerStep)]
    [InlineData("CountInSettings", "bars", 0, PracticeLimits.MaxCountInBars)]
    public void Limits_MatchTheSchemaRanges(string schema, string property, int min, int max)
    {
        var node = Schema(schema)["properties"]![property]!;

        (node["type"]!.GetValue<string>(), Number(node["minimum"]!), Number(node["maximum"]!)).ShouldBe(("integer", min, max));
    }

    [Fact]
    public void PracticeExample_RoundTripsAndValidates()
    {
        var (expected, actual) = RoundTrip<MixSettingsDto>("MixSettingsWithPractice");
        var dto = Example("MixSettingsWithPractice").Deserialize<MixSettingsDto>(Web)!;

        JsonNode.DeepEquals(expected, actual).ShouldBeTrue();
        dto.Practice.ShouldBe(new PracticeSettingsDto(75, -2, new SpeedTrainerSettingsDto(true, 60, 5, 100, 2), new CountInSettingsDto(1, true)));
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("MixSettings"), actual).ShouldBeEmpty();
    }

    [Fact]
    public void MixSettingsBefore1110_LoadWithTheDefaultsAndStayValid()
    {
        var legacy = Example("MixSettings").Deserialize<MixSettingsDto>(Web)!;
        var node = JsonSerializer.SerializeToNode(legacy, Web)!;

        legacy.Practice.ShouldBe(PracticeSettingsDto.Default);
        node["practice"]!["speedPercent"]!.GetValue<int>().ShouldBe(100);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("MixSettings"), node).ShouldBeEmpty();
    }

    [Fact]
    public void PracticeSchemas_AreClosedAndRequireEveryField()
    {
        foreach (var name in AddedSchemas)
        {
            var schema = Schema(name);
            schema["additionalProperties"]!.GetValue<bool>().ShouldBeFalse(name);
            Strings(schema["required"]!).ShouldBe(schema["properties"]!.AsObject().Select(p => p.Key), ignoreOrder: true, name);
        }
    }

    [Fact]
    public void PutMix_OffersThePracticeExample()
    {
        var examples = Section(OpenApiContract.Backend.Root, "paths")["/api/mixes/{mixId}"]!["put"]!["requestBody"]!["content"]!["application/json"]!["examples"]!;

        examples["slowPractice"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/examples/MixSettingsWithPractice");
    }

    private static SpeedTrainerSettingsDto TrainerDefaults()
    {
        var trainer = Schema("SpeedTrainerSettings")["properties"]!;
        return new SpeedTrainerSettingsDto(
            Default<bool>(trainer, "enabled"), Int(trainer, "startPercent"), Int(trainer, "stepPercent"), Int(trainer, "targetPercent"), Int(trainer, "passesPerStep"));
    }

    private static T Default<T>(JsonNode properties, string name) => properties[name]!["default"]!.GetValue<T>();

    // The YAML loader yields JSON numbers as doubles; every practice value is an integer.
    private static int Int(JsonNode properties, string name) => Number(properties[name]!["default"]!);

    private static int Number(JsonNode value) => (int)value.GetValue<double>();

    private static JsonObject Schema1210(string name) => Section(Backend1210.Root, "components/schemas")[name]!.AsObject();
}
