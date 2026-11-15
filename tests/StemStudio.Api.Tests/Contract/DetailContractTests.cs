using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Tests.Contract;

/// <summary>Skeleton tests (architect, feature 002 Phase 0): detail DTOs, names and routes against backend-api.openapi.yaml 1.2.0.</summary>
public sealed class DetailContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("DetailQueuedTrack")]
    [InlineData("DetailCompletedTrack")]
    [InlineData("CompletedTrack")]
    public void TrackExamples_RoundTripThroughTrackDtoAndMatchTheSchema(string example)
    {
        var json = Example(example);

        var dto = json.Deserialize<TrackDto>(Web)!;
        var again = JsonSerializer.SerializeToNode(dto, Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), again).ShouldBeEmpty();
        again!["detail"]!["status"]!.GetValue<string>().ShouldBe(json["detail"]!["status"]!.GetValue<string>());
    }

    [Fact]
    public void CompletedDetailExample_MapsOutcomesAndStems()
    {
        var dto = Example("DetailCompletedTrack").Deserialize<TrackDto>(Web)!;

        dto.Detail.Status.ShouldBe(DetailPhaseStatusDto.Completed);
        dto.Detail.Parents.Select(p => p.Outcome).ShouldBe(
            [ParentOutcomeKindDto.Split, ParentOutcomeKindDto.Split, ParentOutcomeKindDto.NotSupported, ParentOutcomeKindDto.SkippedSilent]);
        dto.Detail.Parents[1].Stems[^1].Name.ShouldBe(DetailNames.Rest);
    }

    [Fact]
    public void TrackDto_DefaultsToTheNoneDetailPhase()
    {
        var node = JsonSerializer.SerializeToNode(DetailPhaseDto.None, Web)!;

        node["status"]!.GetValue<string>().ShouldBe("none");
        node["parents"]!.AsArray().ShouldBeEmpty();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("DetailPhase"), node).ShouldBeEmpty();
    }

    [Fact]
    public void MixSettings_WithoutDetailFieldsLoadEmptyAndWithThemRoundTrip()
    {
        var legacy = Example("MixSettings").AsObject();
        var withDetail = Example("MixSettingsWithDetail");

        var old = legacy.Deserialize<MixSettingsDto>(Web)!;
        var detail = withDetail.Deserialize<MixSettingsDto>(Web)!;

        old.Expanded.ShouldBeEmpty();
        old.DetailChannels.ShouldBeEmpty();
        detail.Expanded.ShouldBe([StemNames.Drums]);
        detail.DetailChannels.ShouldBe([new DetailChannelSettingsDto(StemNames.Drums, "snare", 0.8, 0, true, false)]);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("MixSettings"), JsonSerializer.SerializeToNode(detail, Web)).ShouldBeEmpty();
    }

    [Fact]
    public void DetailNames_MatchTheContractEnums()
    {
        DetailNames.All.ShouldBe(Enum("DetailStemName"));
        DetailNames.Parents.ShouldBe(Enum("ParentStemName"));
    }

    [Theory]
    [InlineData(StemNames.Drums, "kick", 6)]
    [InlineData(StemNames.Vocals, "lead", 3)]
    [InlineData(StemNames.Guitar, "strings", 9)]
    [InlineData(StemNames.Piano, "strings", 9)]
    [InlineData(StemNames.Other, "strings", 9)]
    public void AllowedFor_ListsTheParentsStemsWithRestLast(string parent, string first, int count)
    {
        var allowed = DetailNames.AllowedFor(parent);

        allowed.Count.ShouldBe(count);
        allowed[0].ShouldBe(first);
        allowed[^1].ShouldBe(DetailNames.Rest);
    }

    [Theory]
    [InlineData(StemNames.Bass)]
    [InlineData("unknown")]
    public void AllowedFor_IsEmptyForStemsThatAreNeverSplit(string parent) => DetailNames.AllowedFor(parent).ShouldBeEmpty();

    [Fact]
    public void DetailRoutes_BuildContractPaths()
    {
        var id = Guid.Empty;

        ApiRoutes.StartDetail(id).ShouldBe("/api/tracks/00000000-0000-0000-0000-000000000000/detail");
        ApiRoutes.CancelDetail(id).ShouldBe("/api/tracks/00000000-0000-0000-0000-000000000000/detail/cancel");
        ApiRoutes.DetailStem(id, StemNames.Drums, "kick").ShouldBe("/api/tracks/00000000-0000-0000-0000-000000000000/detail-stems/drums/kick");
    }

    private static JsonNode Schema(string name) => OpenApiContract.Backend.Root["components"]!["schemas"]![name]!;

    private static JsonNode Example(string name) => OpenApiContract.Backend.Root["components"]!["examples"]![name]!["value"]!.DeepClone();

    private static List<string> Enum(string schema) => Schema(schema)["enum"]!.AsArray().Select(v => v!.GetValue<string>()).ToList();
}
