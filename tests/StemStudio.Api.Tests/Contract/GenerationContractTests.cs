using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Styles;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 009 + 010 Phase 0, 009 T005): style and generation DTOs, routes and limits against
/// backend-api 1.8.0 (feature 009) as contained in 1.9.0, the generator 1.0.0/1.1.0 example copies, and the rule
/// 008 (1.7.0) -> 009 (1.8.0) -> 010 (1.9.0): every newer document contains the older one unchanged except the named entries.
/// </summary>
public sealed class GenerationContractTests
{
    private static readonly string[] Entries009 = ["Track", "AssetOrigin", "Provenance", "AudioFacts"];

    private static readonly string[] Entries010 =
    [
        "GenerationKind", "GenerationState", "GenerationErrorCode", "Generation", "GenerationTake", "TrackOrigin", "GenerationStatus",
        "GeneratedTrack", "ReadyGenerationStatus", "NotInstalledGenerationStatus", "UnavailableGenerationStatus", "CompletedGeneration",
        "/api/generations/{generationId}",
    ];

    private static readonly (Type Enum, string Schema)[] EnumSchemas =
    [
        (typeof(StyleKindDto), "StyleKind"), (typeof(GenreFamilyDto), "GenreFamily"), (typeof(TempoFeelDto), "TempoFeel"),
        (typeof(VocalHandlingDto), "VocalHandling"), (typeof(StrengthDto), "Strength"), (typeof(GenerationAvailabilityDto), "GenerationAvailability"),
        (typeof(GenerationKindDto), "GenerationKind"), (typeof(GenerationStateDto), "GenerationState"), (typeof(GenerationErrorCodeDto), "GenerationErrorCode"),
        (typeof(RestyleModeDto), "RestyleMode"), (typeof(TrackOriginKindDto), "TrackOriginKind"), (typeof(AssetOriginDto), "AssetOrigin"),
    ];

    [Fact]
    public void Contracts_HaveTheVersionsOfFeatures009And010()
    {
        Backend009.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.8.0");
        OpenApiContract.Backend.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.9.0");
        Generator009.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.0.0");
        GeneratorContract.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.1.0");
    }

    [Theory]
    [MemberData(nameof(Sections), MemberType = typeof(ContractJson))]
    public void NewerContracts_ContainTheOlderOnesUnchangedExceptTheNamedEntries(string section)
    {
        ChangedEntries(Backend008, Backend009, section).ShouldBeSubsetOf(Entries009);
        ChangedEntries(Backend009, OpenApiContract.Backend, section).ShouldBeSubsetOf(Entries010);
    }

    [Fact]
    public void GenerationEnums_MatchTheContract() => AssertEnumsMatch(EnumSchemas);

    [Theory]
    [InlineData("PresetStyle")]
    [InlineData("UserStyle")]
    public void StyleExamples_RoundTripThroughStyleDto(string example) => AssertRoundTrip<StyleDto>(example, "Style");

    [Fact]
    public void StyleInputExample_RoundTripsThroughStyleInput() => AssertRoundTrip<StyleInput>("StyleInputExample", "StyleInput");

    [Theory]
    [InlineData("RangeRestyleRequest")]
    [InlineData("InPlaceRestyleRequest")]
    public void RestyleRequests_RoundTrip(string example) => AssertRoundTrip<RestyleRequest>(example, "RestyleRequest");

    [Theory]
    [InlineData("ReadyGenerationStatus")]
    [InlineData("NotInstalledGenerationStatus")]
    [InlineData("UnavailableGenerationStatus")]
    public void StatusExamples_RoundTrip(string example) => AssertRoundTrip<GenerationStatusDto>(example, "GenerationStatus");

    [Theory]
    [InlineData("QueuedGeneration")]
    [InlineData("RunningGeneration")]
    [InlineData("CompletedGeneration")]
    [InlineData("FailedGeneration")]
    public void RestyleGenerations_RoundTrip(string example) => AssertRoundTrip<GenerationDto>(example, "Generation");

    [Fact]
    public void GeneratedTrack_RoundTripsWithItsOrigin()
    {
        AssertRoundTrip<TrackDto>("GeneratedTrack", "Track");
        var origin = Example("GeneratedTrack").Deserialize<TrackDto>(Web)!.Origin!;

        (origin.Kind, origin.TakeIndex, origin.AiGenerated).ShouldBe((TrackOriginKindDto.Generated, 2, true));
    }

    [Fact]
    public void ImportedTracks_HaveNoOriginOnTheWire()
    {
        var json = JsonSerializer.SerializeToNode(Example("CompletedTrack").Deserialize<TrackDto>(Web), Web)!.AsObject();

        json.ContainsKey("origin").ShouldBeFalse();
    }

    [Fact]
    public void ExportProvenance_HasNoGenerationMemberOnTheWire()
    {
        var json = JsonSerializer.SerializeToNode(Example("ReadyAsset").Deserialize<AssetDto>(Web), Web)!;

        json["provenance"]!.AsObject().ContainsKey("generation").ShouldBeFalse();
    }

    [Fact]
    public void CompletedGeneration_TakesCarryTheirUrlsAndTheAiLabel()
    {
        var dto = Example("CompletedGeneration").Deserialize<GenerationDto>(Web)!;

        dto.Takes.Select(t => t.AudioUrl).ShouldBe([ApiRoutes.TakeAudio(dto.Id, 1), ApiRoutes.TakeAudio(dto.Id, 2)]);
        dto.Takes.ShouldAllBe(t => t.AiGenerated && t.Vocal == null);
        (dto.Restyle!.Style.PresetKey, dto.Restyle.Range.Mode).ShouldBe(("jazz-trio", RestyleModeDto.Range));
    }

    [Fact]
    public void Routes_ExistInTheContract()
    {
        var id = Guid.NewGuid();
        var routes = new[]
        {
            (ApiRoutes.GenerationStatus, "/api/generation/status"), (ApiRoutes.Styles, "/api/styles"), (ApiRoutes.Style(id), "/api/styles/{styleId}"),
            (ApiRoutes.DuplicateStyle(id), "/api/styles/{styleId}/duplicate"), (ApiRoutes.Restyles(id), "/api/tracks/{trackId}/restyles"),
            (ApiRoutes.Generations, "/api/generations"), (ApiRoutes.Generation(id), "/api/generations/{generationId}"),
            (ApiRoutes.CancelGeneration(id), "/api/generations/{generationId}/cancel"), (ApiRoutes.RetryGeneration(id), "/api/generations/{generationId}/retry"),
            (ApiRoutes.TakeAudio(id, 1), "/api/generations/{generationId}/takes/{index}/audio"),
            (ApiRoutes.ImportTake(id, 2), "/api/generations/{generationId}/takes/{index}/import"),
        };

        AssertRoutesExist(routes, id);
    }


    [Fact]
    public void StyleLimits_MatchTheContract()
    {
        var style = Schema("StyleInput")["properties"]!;

        style["name"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.StyleNameMaxLength);
        style["subGenre"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.StyleSubGenreMaxLength);
        style["description"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.StyleDescriptionMaxLength);
        style["moods"]!["maxItems"]!.GetValue<double>().ShouldBe(ContractLimits.StyleMaxTags);
        Schema("StyleTag")["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.StyleTagMaxLength);
        Schema("RestyleDetails")["properties"]!["caption"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.CaptionMaxLength);
        Schema("RestyleRequest")["properties"]!["takes"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.MaxTakes);
    }

    [Theory]
    [InlineData("ReadyHealth")]
    [InlineData("NoGpuHealth")]
    [InlineData("SingerReadyHealth")]
    [InlineData("RangeRestyleParams")]
    [InlineData("InPlaceRestyleParams")]
    [InlineData("QueuedRestyle")]
    [InlineData("WaitingForGpuRestyle")]
    [InlineData("RunningRestyle")]
    [InlineData("CompletedRestyle")]
    [InlineData("FailedOutOfMemory")]
    [InlineData("CancelledRestyle")]
    public void GeneratorExampleCopies_Are009AndCommonExamplesOfTheContract(string file) =>
        IsGeneratorCopyOf(file, GeneratorJson(file)).ShouldBeTrue(file);

    [Fact]
    public void GeneratorExamples_CoverEveryContractExample()
    {
        var names = GeneratorContract.Root["components"]!["examples"]!.AsObject().Select(e => e.Key.Replace("Upload", "Params", StringComparison.Ordinal));
        var files = Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Generator", "Examples"), "*.json").Select(Path.GetFileNameWithoutExtension);

        files.ShouldBe(names, ignoreOrder: true);
    }

    /// <summary>The generator example; for multipart uploads only the JSON <c>params</c> part.</summary>
    internal static JsonNode GeneratorJson(string file)
    {
        var upload = file.Replace("Params", "Upload", StringComparison.Ordinal);
        return file == upload ? GeneratorExample(file) : GeneratorExample(upload)["params"]!;
    }

    internal static void AssertRoundTrip<T>(string example, string schema)
    {
        var (expected, actual) = RoundTripDeep<T>(example);

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(example);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), JsonSerializer.SerializeToNode(Example(example).Deserialize<T>(Web), Web)).ShouldBeEmpty();
    }

    /// <summary>Asserts that every route fills its template and the template is a path of the backend contract.</summary>
    internal static void AssertRoutesExist(IEnumerable<(string Route, string Template)> routes, Guid id)
    {
        foreach (var (route, template) in routes)
        {
            RouteMatches(route, template, id).ShouldBeTrue(route);
            Section(OpenApiContract.Backend.Root, "paths").ContainsKey(template).ShouldBeTrue(template);
        }
    }

    /// <summary>True when <paramref name="route"/> is <paramref name="template"/> with every id = <paramref name="id"/> and the take index 1 or 2.</summary>
    internal static bool RouteMatches(string route, string template, Guid id) =>
        new[] { "1", "2" }.Any(index => route == string.Join('/', template.Split('/').Select(segment => Fill(segment, id, index))));

    private static string Fill(string segment, Guid id, string index) =>
        segment switch
        {
            "{index}" => index,
            _ when segment.StartsWith('{') => id.ToString(),
            _ => segment,
        };
}
