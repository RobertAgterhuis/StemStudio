using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 020 Phase 0, T002): the Handouts DTOs, the asset additions (<c>kind</c>/<c>origin</c> <c>handout</c>,
/// <c>Asset.handout</c>, <c>Provenance.handoutRecipe</c>) and the practice note DTOs match backend-api 1.19.0.
/// </summary>
public sealed class HandoutContractTests
{
    private static readonly Guid TrackId = Guid.Parse("7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f");

    public static TheoryData<string, object> SampleInstances => new()
    {
        { "HandoutFacts", Facts() },
        { "HandoutRecipe", Recipe() },
        { "HandoutSource", HandoutSourceDto.Song(TrackId) },
        { "CreateHandoutRequest", new CreateHandoutRequest { Source = HandoutSourceDto.Setlist(TrackId), Family = HandoutFamilyDto.Overview } },
        {
            "CreateHandoutRequest", new CreateHandoutRequest
            {
                Source = HandoutSourceDto.Song(TrackId),
                Family = HandoutFamilyDto.Melodic,
                Include = [IngredientCodeDto.Facts, IngredientCodeDto.Notation],
                NotationAssetIds = [TrackId],
                Transposition = TranspositionDto.EFlat,
                Paper = PaperDto.Letter,
                Name = "Lesson",
            }
        },
        {
            "PracticeNotes", new PracticeNotesDto
            {
                Notes = [new() { Family = HandoutFamilyDto.Bass, Text = "Keep the eighths even.", UpdatedAt = DateTimeOffset.UnixEpoch }],
            }
        },
        { "SetPracticeNoteRequest", new SetPracticeNoteRequest { Text = string.Empty } },
    };

    public static TheoryData<string> CreateExamples => new("oneClickBass", "melodicBFlat", "setlist");

    [Fact]
    public void BassHandoutOptionsExample_RoundTripsAndValidates()
    {
        var json = Example("BassHandoutOptions");
        var again = JsonSerializer.SerializeToNode(json.Deserialize<HandoutOptionsDto>(Web), Web)!;

        JsonNode.DeepEquals(json, again).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("HandoutOptions"), again).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(CreateExamples))]
    public void CreateHandoutExamples_DeserializeAndValidateAgain(string name)
    {
        var body = OpenApiContract.Backend.Operation("createHandout")["requestBody"]!["content"]!["application/json"]!;
        var json = body["examples"]![name]!["value"]!;
        var dto = json.Deserialize<CreateHandoutRequest>(Web)!;
        var again = JsonSerializer.SerializeToNode(dto, Web)!;

        (dto.Source.TrackId is null).ShouldNotBe(dto.Source.SetlistId is null, name);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("CreateHandoutRequest"), again).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(SampleInstances))]
    public void ConstructedDtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var node = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), node).ShouldBeEmpty();
    }

    [Fact]
    public void HandoutSource_WritesExactlyOneMember()
    {
        var node = JsonSerializer.SerializeToNode(HandoutSourceDto.Setlist(TrackId), Web)!.AsObject();

        node.Select(pair => pair.Key).ShouldBe(["setlistId"]);
    }

    [Fact]
    public void Provenance_WithARecipe_ValidatesAndOtherKindsOmitIt()
    {
        var handout = Provenance() with { HandoutRecipe = Recipe() };
        var node = JsonSerializer.SerializeToNode(handout, Web)!;

        node["handoutRecipe"]!["kind"]!.GetValue<string>().ShouldBe("handout");
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Provenance"), node).ShouldBeEmpty();
        JsonSerializer.SerializeToNode(Provenance(), Web)!.AsObject().ContainsKey("handoutRecipe").ShouldBeFalse();
    }

    [Fact]
    public void AssetEnums_EndWithHandout()
    {
        AssertEnumsMatch([(typeof(AssetKindDto), "AssetKind"), (typeof(AssetOriginDto), "AssetOrigin")]);
        WireNames(typeof(AssetKindDto)).Last().ShouldBe("handout");
        WireNames(typeof(AssetOriginDto)).Last().ShouldBe("handout");
    }

    [Fact]
    public void ComponentEnums_MatchTheirSchemas()
    {
        AssertEnumsMatch([
            (typeof(HandoutFamilyDto), "HandoutFamily"), (typeof(TranspositionDto), "Transposition"), (typeof(PaperDto), "Paper"),
            (typeof(IngredientCodeDto), "IngredientCode"), (typeof(IngredientStatusDto), "IngredientStatus"),
            (typeof(IngredientActionDto), "IngredientAction"),
        ]);
    }

    [Fact]
    public void InlineEnums_MatchTheirSchemas()
    {
        WireNames(typeof(NotationKindDto)).ShouldBe(Strings(Schema("NotationChoice")["properties"]!["kind"]!["enum"]!));
        WireNames(typeof(HandoutKindDto)).ShouldBe(Strings(Schema("HandoutOptions")["properties"]!["kind"]!["enum"]!));
        WireNames(typeof(HandoutKindDto)).ShouldBe(Strings(Schema("HandoutFacts")["properties"]!["kind"]!["enum"]!));
        WireNames(typeof(HandoutKindDto)).ShouldBe(Strings(Schema("HandoutRecipe")["properties"]!["kindLabel"]!["enum"]!));
        WireNames(typeof(OmissionReasonDto)).ShouldBe(Strings(Schema("OmittedIngredient")["properties"]!["reason"]!["enum"]!));
        WireNames(typeof(HandoutWarningCodeDto)).ShouldBe(Strings(Schema("HandoutWarning")["properties"]!["code"]!["enum"]!));
        Schema("HandoutRecipe")["properties"]!["kind"]!["const"]!.GetValue<string>().ShouldBe(HandoutRecipeDto.RecipeKind);
    }

    [Fact]
    public void BackendWarnings_AreTheWorkerWarnings()
    {
        var worker = WorkerContract.Root["components"]!["schemas"]!["HandoutWarningCode"]!["enum"]!;

        WireNames(typeof(HandoutWarningCodeDto)).ShouldBe(Strings(worker));
    }

    private static HandoutFactsDto Facts() => new()
    {
        Kind = HandoutKindDto.Song,
        Family = HandoutFamilyDto.Bass,
        Transposition = null,
        Paper = PaperDto.A4,
        Pages = 5,
        Songs = 1,
        Included = [IngredientCodeDto.Facts, IngredientCodeDto.FormChart],
        Omitted = [new() { Code = IngredientCodeDto.Notation, Reason = OmissionReasonDto.NotAvailable }],
        Warnings = [new() { Code = HandoutWarningCodeDto.NotationScaled, Text = "Notation pages were scaled to Letter." }],
        Outline = ["Example Song", "Form"],
    };

    private static HandoutRecipeDto Recipe() => new()
    {
        Kind = HandoutRecipeDto.RecipeKind,
        KindLabel = HandoutKindDto.Song,
        TrackId = TrackId,
        SetlistId = null,
        SetlistName = null,
        Family = HandoutFamilyDto.Bass,
        Include = null,
        NotationAssetIds = null,
        Transposition = null,
        Paper = PaperDto.A4,
    };

    private static ProvenanceDto Provenance() => new()
    {
        Origin = AssetOriginDto.Handout,
        TrackId = TrackId,
        TrackTitle = "Example Song",
        TrackArtist = "Example Band",
        TrackAvailable = true,
        SourceAssetIds = [],
        Contents = "Bass handout",
        Recipe = null,
        ScoreRecipe = null,
    };
}
