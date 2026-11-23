using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 022 Phase 0, T002/T003): backend-api 1.22.0 (<c>specs/022-library-folders/contracts/backend-api.openapi.yaml</c>)
/// is the newest full document (<see cref="Base"/>, 1.12.0 of 013; 1.11.0 until the 022+023 round merged 013) plus exactly the 022 delta (DELTA.md section 1, <c>merge_full.py</c>),
/// and the library DTOs, enums, routes, problem types and limits match it. When the CTO re-merges 1.22.0 onto 013's 1.12.0,
/// only <see cref="Base"/> is re-pointed (every expectation is computed relative to it).
/// </summary>
public sealed partial class LibraryContractTests
{
    private const string ExternalRef = "../../011-ai-composer-agent/contracts/backend-api.openapi.yaml#";

    // DELTA.md section 1 item 2: shared with the base (same meaning), not added again.
    private static readonly Dictionary<string, string[]> Shared = new()
    {
        ["components/parameters"] = ["TrackId", "AssetId"],
        ["components/responses"] = ["NotFound"],
        ["components/schemas"] = ["Asset"],
    };

    /// <summary>The full document 1.22.0 was merged onto (012's 1.11.0; 013's 1.12.0 after the CTO re-merge).</summary>
    private static OpenApiContract Base => Backend013;

    // The 1.22.0 document under test (linked as Contract/022); the current full document is 023's 1.23.0 on top of it.
    private static OpenApiContract Merged => Backend022;

    private static OpenApiContract Delta => Backend022Delta;

    public static TheoryData<string> MergedSections => Sections;

    [Fact]
    public void Backend_IsLabelled1220AndListsTheMergedDeltas()
    {
        var info = Merged.Root["info"]!;
        var contains = Strings(info["x-stemstudio-contains"]!).ToList();

        info["version"]!.GetValue<string>().ShouldBe("1.22.0");
        contains.ShouldBe([.. Strings(Base.Root["info"]!["x-stemstudio-contains"]!), "1.22.0"]);
        Delta.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.22.0-delta.1");
    }

    [Theory]
    [MemberData(nameof(MergedSections))]
    public void Backend1220_ContainsTheBaseUnchangedExceptAssetAndItsExamples(string section)
    {
        string[] expected = section switch
        {
            "components/schemas" => ["Asset"],
            "components/examples" => [.. BaseAssetExamples()],
            _ => [],
        };

        ChangedEntries(Base, Merged, section).ShouldBe(expected, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(MergedSections))]
    public void Backend1220_AddsExactlyTheDeltaEntries(string section)
    {
        var added = Keys(Merged, section).Except(Keys(Base, section));
        var shared = Shared.GetValueOrDefault(section, []);

        added.ShouldBe(Keys(Delta, section).Except(shared), ignoreOrder: true);
        shared.ShouldBeSubsetOf(Keys(Base, section));
    }

    [Theory]
    [MemberData(nameof(MergedSections))]
    public void Backend1220_ContainsEveryDeltaEntryUnchanged(string section)
    {
        var merged = Section(Merged.Root, section);
        var shared = Shared.GetValueOrDefault(section, []);

        foreach (var (name, value) in Section(Delta.Root, section).Where(entry => !shared.Contains(entry.Key)))
        {
            JsonNode.DeepEquals(Localized(value), merged[name]).ShouldBeTrue($"{section}/{name}");
        }
    }

    [Fact]
    public void Backend1220_AddsTheNineLibraryOperationsAndTheTag()
    {
        var added = Merged.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>())
            .Except(Base.Operations.Select(op => op.Operation["operationId"]!.GetValue<string>()));

        added.ShouldBe(LibraryOperations, ignoreOrder: true);
        Merged.Root["tags"]!.AsArray().Select(tag => tag!["name"]!.GetValue<string>()).ShouldContain("library");
        Merged.Operations.Where(op => LibraryOperations.Contains(op.Operation["operationId"]!.GetValue<string>()))
            .ShouldAllBe(op => Strings(op.Operation["tags"]!).Single() == "library");
    }

    private static JsonNode Asset1150 => Backend016.Root["components"]!["schemas"]!["Asset"]!;

    [Fact]
    public void Asset_GainsOnlyTheRequiredPlacementOfTheDelta()
    {
        // 020 added Asset.handout (1.19.0); the 022 placement rule is checked on 016's 1.15.0, the last document before it.
        var after = Asset1150.DeepClone().AsObject();
        var required = after["required"]!.AsArray();

        JsonNode.DeepEquals(after["properties"]!["placement"], Localized(Delta.Root["components"]!["schemas"]!["Asset"]!["properties"]!["placement"]!)).ShouldBeTrue();
        Strings(required).ToList().IndexOf("placement").ShouldBe(Strings(required).ToList().IndexOf("provenance") + 1);
        after["properties"]!.AsObject().Remove("placement");
        required.Remove(required.Single(node => node!.GetValue<string>() == "placement"));
        JsonNode.DeepEquals(after, Base.Root["components"]!["schemas"]!["Asset"]).ShouldBeTrue();
    }

    [Fact]
    public void Asset_PropertiesOfTheDeltaEqualTheBaseAndPlacement()
    {
        var delta = Delta.Root["components"]!["schemas"]!["Asset"]!["properties"]!.AsObject().Select(p => p.Key);

        delta.ShouldBe(Asset1150["properties"]!.AsObject().Select(p => p.Key), ignoreOrder: true);
        Strings(Delta.Root["components"]!["schemas"]!["Asset"]!["required"]!).ShouldBe(Strings(Asset1150["required"]!));
    }

    [Fact]
    public void AssetExamples_GainOnlyAValidPlacementWithWellFormedFolderIds()
    {
        foreach (var name in BaseAssetExamples())
        {
            var merged = Example(name).AsObject();

            AssertFolderIds(merged["placement"]!, name);
            merged.Remove("placement");
            JsonNode.DeepEquals(merged, Base.Root["components"]!["examples"]![name]!["value"]).ShouldBeTrue(name);
            SchemaValidator.Validate(Merged, Schema("Asset"), Example(name)).ShouldBeEmpty(name);
        }
    }

    [Theory]
    [InlineData("RootView", "LibraryFolderView")]
    [InlineData("SongView", "LibraryFolderView")]
    [InlineData("EmptySearch", "LibrarySearchResult")]
    public void DeltaExamples_ValidateAgainstTheirSchemas(string example, string schema)
    {
        SchemaValidator.Validate(Merged, Schema(schema), Example(example)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("RootView")]
    [InlineData("SongView")]
    public void FolderViewExamples_RoundTripThroughLibraryFolderViewDto(string example)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize<LibraryFolderViewDto>(Web), Web)!;

        // latestAt: DateTimeOffset writes "Z" as "+00:00" (same instant); the schema check covers the format.
        JsonNode.DeepEquals(WithoutLatestAt(json), WithoutLatestAt(again)).ShouldBeTrue();
        SchemaValidator.Validate(Merged, Schema("LibraryFolderView"), again).ShouldBeEmpty();
    }

    [Fact]
    public void EmptySearch_RoundTripsThroughLibrarySearchResultDto()
    {
        var (expected, actual) = RoundTrip<LibrarySearchResultDto>("EmptySearch");

        JsonNode.DeepEquals(expected, actual).ShouldBeTrue();
    }

    [Theory]
    [InlineData("QueuedAsset")]
    [InlineData("ReadyScoreAsset")]
    public void AssetExamples_CarryThePlacementThroughAssetDto(string example)
    {
        var dto = Example(example).Deserialize<AssetDto>(Web)!;

        dto.Placement.ShouldNotBeNull();
        dto.Placement.Artist.ShouldBe(new PlacementLevelDto { FolderId = dto.Placement.Artist.FolderId, Name = "Rick Astley", Source = PlacementSourceDto.Import });
        dto.Placement.Album.Source.ShouldBe(PlacementSourceDto.Fallback);
        dto.Placement.Filed.ShouldBeTrue();
    }

    [Fact]
    public void AssetWithoutPlacement_WritesAnExplicitNull()
    {
        var dto = Example("QueuedAsset").Deserialize<AssetDto>(Web)! with { Placement = null };
        var json = JsonSerializer.SerializeToNode(dto, Web)!.AsObject();

        json.ContainsKey("placement").ShouldBeTrue();
        json["placement"].ShouldBeNull();
        SchemaValidator.Validate(Merged, Schema("Asset"), json).ShouldBeEmpty();
    }

    [Fact]
    public void Enums_MatchTheContract()
    {
        AssertEnumsMatch([
            (typeof(FolderLevelDto), "FolderLevel"),
            (typeof(FolderSortDto), "FolderSort"),
            (typeof(PlacementSourceDto), "PlacementSource"),
            (typeof(FolderArtKindDto), "FolderArtKind"),
            (typeof(SearchFieldDto), "SearchField"),
        ]);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Samples_ValidateAndWriteExactlyTheSchemaProperties(string schema, object dto)
    {
        var json = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)!;

        SchemaValidator.Validate(Merged, Schema(schema), json).ShouldBeEmpty();
        json.AsObject().Select(p => p.Key).ShouldBe(Schema(schema)["properties"]!.AsObject().Select(p => p.Key), ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Samples_RoundTripFromTheWire(string schema, object dto)
    {
        var json = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)!;
        var again = JsonSerializer.SerializeToNode(json.Deserialize(dto.GetType(), Web), dto.GetType(), Web);

        JsonNode.DeepEquals(WithoutLatestAt(WithoutAnyTimes(json)), WithoutLatestAt(WithoutAnyTimes(again!))).ShouldBeTrue(schema);
    }

    [Fact]
    public void OptionalMembers_AreAbsentWhenNull()
    {
        var counts = JsonSerializer.SerializeToNode(new FolderCountsDto { Items = 3 }, Web)!.AsObject();
        var highlight = JsonSerializer.SerializeToNode(new HighlightDto { Field = SearchFieldDto.Name, Start = 0, Length = 2 }, Web)!.AsObject();

        counts.Select(p => p.Key).ShouldBe(["items"]);
        highlight.Select(p => p.Key).ShouldBe(["field", "start", "length"]);
        SchemaValidator.Validate(Merged, Schema("FolderCounts"), counts).ShouldBeEmpty();
        SchemaValidator.Validate(Merged, Schema("Highlight"), highlight).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{"artist":null,"album":"Ace of Spades"}""")]
    [InlineData("""{"album":null,"song":null}""")]
    public void LibraryInfoRequest_RequiresAllThreeMembers(string body)
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<LibraryInfoRequest>(body, Web));
    }

    [Fact]
    public void LibraryInfoRequest_ExamplesAndAutomaticValidate()
    {
        foreach (var operation in new[] { "setTrackLibraryInfo", "setAssetLibraryInfo" })
        {
            var example = Merged.Operation(operation)["requestBody"]!["content"]!["application/json"]!["example"]!;
            var dto = example.Deserialize<LibraryInfoRequest>(Web)!;

            JsonNode.DeepEquals(JsonSerializer.SerializeToNode(dto, Web), example).ShouldBeTrue(operation);
        }

        var automatic = JsonSerializer.SerializeToNode(LibraryInfoRequest.Automatic, Web)!;
        automatic.ToJsonString().ShouldBe("""{"artist":null,"album":null,"song":null}""");
        SchemaValidator.Validate(Merged, Schema("LibraryInfoRequest"), automatic).ShouldBeEmpty();
    }

    [Fact]
    public void Routes_AreContractPaths()
    {
        var paths = Section(Merged.Root, "paths").Select(p => p.Key).ToList();
        var id = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");

        paths.ShouldContain(ApiRoutes.LibraryFolders);
        paths.ShouldContain(ApiRoutes.LibrarySearch);
        paths.ShouldContain(ApiRoutes.LibraryFolder("{folderId}"));
        paths.ShouldContain(ApiRoutes.TrackLibraryInfo(id).Replace(id.ToString(), "{trackId}", StringComparison.Ordinal));
        paths.ShouldContain(ApiRoutes.AssetLibraryInfo(id).Replace(id.ToString(), "{assetId}", StringComparison.Ordinal));
        ApiRoutes.LibraryFolder("ar0123456789abcdef").ShouldBe("/api/library/folders/ar0123456789abcdef");
    }

    [Fact]
    public void LibraryETag_MatchesTheHeaderPattern()
    {
        var pattern = Merged.Root["components"]!["headers"]!["LibraryETag"]!["schema"]!["pattern"]!.GetValue<string>();

        Regex.IsMatch(ApiRoutes.LibraryETag("3f9a0c1d2e4b5a69"), pattern).ShouldBeTrue();
        ApiRoutes.LibraryETag("3f9a0c1d2e4b5a69").ShouldStartWith(ApiRoutes.LibraryETagPrefix);
    }

    [Theory]
    [InlineData("FolderNotFound", LibraryProblemTypes.FolderNotFound)]
    [InlineData("LibraryQueryInvalid", LibraryProblemTypes.LibraryQueryInvalid)]
    [InlineData("NotFileable", LibraryProblemTypes.NotFileable)]
    public void ProblemTypes_MatchTheResponseExamples(string response, string code)
    {
        var example = Merged.Root["components"]!["responses"]![response]!["content"]!["application/problem+json"]!["example"]!;

        example["type"]!.GetValue<string>().ShouldBe(LibraryProblemTypes.Uri(code));
    }

    [Theory]
    [InlineData("LibraryName", "maxLength", ContractLimits.LibraryNameMaxLength)]
    [InlineData("LibrarySearchResult/properties/tokens", "maxItems", ContractLimits.LibrarySearchMaxTokens)]
    [InlineData("LibraryFolderView/properties/items", "maxItems", ContractLimits.LibrarySongItemsMax)]
    [InlineData("LibraryFolderView/properties/children", "maxItems", ContractLimits.LibraryFolderPageMax)]
    [InlineData("LibraryFolderView/properties/breadcrumb", "maxItems", ContractLimits.LibraryBreadcrumbMax)]
    [InlineData("FolderArt/properties/images", "maxItems", ContractLimits.FolderArtMosaicMax)]
    [InlineData("ArtPlaceholder/properties/tone", "maximum", ContractLimits.ArtToneCount - 1)]
    [InlineData("LibraryFolder/properties/trackIds", "maxItems", ContractLimits.FolderTrackIdsMax)]
    [InlineData("ItemHit/properties/highlights", "maxItems", ContractLimits.SearchHighlightsMax)]
    [InlineData("ItemHit/properties/matchedInstruments", "maxItems", ContractLimits.MatchedInstrumentsMax)]
    [InlineData("ItemHit/properties/matchedInstruments/items", "maxLength", ContractLimits.InstrumentLabelMaxLength)]
    [InlineData("Placement/properties/year", "minimum", ContractLimits.LibraryYearMin)]
    [InlineData("Placement/properties/year", "maximum", ContractLimits.LibraryYearMax)]
    public void SchemaLimits_MatchContractLimits(string path, string member, int expected)
    {
        var node = path.Split('/').Skip(1).Aggregate(Schema(path.Split('/')[0]), (current, part) => current[part]!);

        Int(node, member).ShouldBe(expected);
    }

    [Fact]
    public void ParameterLimitsAndPatterns_MatchContractLimits()
    {
        var folderLimit = Merged.Root["components"]!["parameters"]!["FolderLimit"]!["schema"]!;
        var search = Merged.Operation("searchLibrary")["parameters"]!.AsArray();
        JsonNode Query(string name) => search.Single(p => p!["name"]?.GetValue<string>() == name)!["schema"]!;

        (Int(folderLimit, "maximum"), Int(folderLimit, "default")).ShouldBe((ContractLimits.LibraryFolderPageMax, ContractLimits.LibraryFolderPageDefault));
        Int(Query("q"), "maxLength").ShouldBe(ContractLimits.LibrarySearchMaxLength);
        (Int(Query("limit"), "maximum"), Int(Query("limit"), "default")).ShouldBe((ContractLimits.LibrarySearchGroupMax, ContractLimits.LibrarySearchGroupDefault));
        Schema("FolderId")["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.FolderIdPattern);
        Schema("LibraryFolderView")["properties"]!["revision"]!["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.LibraryRevisionPattern);
    }

    private static int Int(JsonNode node, string member) => (int)double.Parse(node[member]!.ToJsonString(), CultureInfo.InvariantCulture);

    private static JsonNode WithoutLatestAt(JsonNode node)
    {
        var copy = node.DeepClone();
        Visit(copy, obj => obj.Remove("latestAt"));
        return copy;
    }

    /// <summary>Calls <paramref name="visit"/> on every object of a tree (depth first, parents before children).</summary>
    private static void Visit(JsonNode? node, Action<JsonObject> visit)
    {
        if (node is JsonObject obj)
        {
            visit(obj);
        }

        var children = node switch
        {
            JsonObject members => members.Select(pair => pair.Value).ToList(),
            JsonArray array => [.. array],
            _ => [],
        };
        children.ForEach(child => Visit(child, visit));
    }

    private static IEnumerable<string> Keys(OpenApiContract contract, string section) => Section(contract.Root, section).Select(entry => entry.Key);

    /// <summary>The base examples whose value is an <c>Asset</c> (every 1.11.0 <c>Asset</c> member present).</summary>
    private static IEnumerable<string> BaseAssetExamples()
    {
        var required = Strings(Base.Root["components"]!["schemas"]!["Asset"]!["required"]!).ToList();
        return Section(Base.Root, "components/examples")
            .Where(entry => entry.Value!["value"] is JsonObject value && required.All(value.ContainsKey))
            .Select(entry => entry.Key);
    }

    /// <summary>A copy of a delta node whose external <c>$ref</c>s to the 011 document are local.</summary>
    private static JsonNode? Localized(JsonNode? node)
    {
        var copy = node?.DeepClone();
        Visit(copy, obj =>
        {
            if (obj["$ref"] is JsonValue reference && reference.GetValue<string>().StartsWith(ExternalRef, StringComparison.Ordinal))
            {
                obj["$ref"] = "#" + reference.GetValue<string>()[ExternalRef.Length..];
            }
        });
        return copy;
    }

    private static void AssertFolderIds(JsonNode placement, string example)
    {
        foreach (var (level, prefix) in new[] { ("artist", "ar"), ("album", "al"), ("song", "so") })
        {
            var id = placement[level]!["folderId"]!.GetValue<string>();
            (id.StartsWith(prefix, StringComparison.Ordinal) && FolderIdRegex().IsMatch(id)).ShouldBeTrue($"{example} {level}");
        }
    }

    [GeneratedRegex(ContractLimits.FolderIdPattern)]
    private static partial Regex FolderIdRegex();
}
