using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 005 + 007 Phase 0, 007 T002): asset DTOs, routes and limits against
/// backend-api 1.5.0, the worker 1.4.0 render examples and render-vectors 1.0.0.
/// </summary>
public sealed class AssetContractTests
{
    private const string TrackId = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a";

    private static readonly Lazy<JsonNode> Vectors =
        new(() => JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "render-vectors.json")))!);

    [Fact]
    public void Contracts_HaveTheVersionsOfFeature007()
    {
        Backend007.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.5.0");
        Vectors.Value["version"]!.GetValue<string>().ShouldBe("1.0.0");
    }

    [Theory]
    [InlineData(typeof(AssetKindDto), "AssetKind")]
    [InlineData(typeof(AssetStatusDto), "AssetStatus")]
    [InlineData(typeof(AssetFileRoleDto), "AssetFileRole")]
    [InlineData(typeof(AssetOriginDto), "AssetOrigin")]
    [InlineData(typeof(ExportModeDto), "ExportMode")]
    [InlineData(typeof(AudioFileFormatDto), "AudioFileFormat")]
    public void Enums_MatchTheContract(Type enumType, string schema)
    {
        WireNames(enumType).ShouldBe(Strings(Schema(schema)["enum"]!));
    }

    [Theory]
    [InlineData("QueuedAsset")]
    [InlineData("RenderingAsset")]
    [InlineData("ReadyAsset")]
    [InlineData("FailedAsset")]
    public void AssetExamples_RoundTripThroughAssetDto(string example)
    {
        var json = Example(example);

        var again = JsonSerializer.SerializeToNode(json.Deserialize<AssetDto>(Web), Web)!;

        JsonNode.DeepEquals(WithoutTimes(again), WithoutTimes(json)).ShouldBeTrue();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Asset"), again).ShouldBeEmpty();
    }

    [Fact]
    public void ReadyAsset_ExposesTheGainNoteFactsAndTheFileRoute()
    {
        var dto = Example("ReadyAsset").Deserialize<AssetDto>(Web)!;

        dto.Audio!.AppliedGainDb.ShouldBe(-1.8);
        dto.Provenance.TrackAvailable.ShouldBeFalse();
        dto.Provenance.Recipe!.Mix!.Expanded.ShouldBe(["drums"]);
        dto.Files.Single().Url.ShouldBe(ApiRoutes.AssetFile(dto.Id, ApiRoutes.AssetFileAudio));
    }

    [Theory]
    [InlineData("RawStemExportRequest")]
    [InlineData("AudibleMixExportRequest")]
    [InlineData("SelectedChannelsExportRequest")]
    public void ExportRequestExamples_RoundTripThroughCreateAudioExportRequest(string example)
    {
        var json = Example(example);

        var again = JsonSerializer.SerializeToNode(json.Deserialize<CreateAudioExportRequest>(Web), Web);

        JsonNode.DeepEquals(again, json).ShouldBeTrue();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("CreateAudioExportRequest"), again).ShouldBeEmpty();
    }

    [Fact]
    public void OtherBodies_MatchTheirSchemas()
    {
        var asset = Example("QueuedAsset").Deserialize<AssetDto>(Web)!;
        var format = new ExportFormatDto(AudioFileFormatDto.Flac, 16);

        Valid("CreateStemPackRequest", new CreateStemPackRequest(asset.Provenance.TrackId!.Value, format, IncludeDetail: true));
        Valid("StemPack", new StemPackDto([asset]));
        Valid("RenameAssetRequest", new RenameAssetRequest("Drums for practice"));
        Valid("AssetList", new AssetListDto([asset], 1, 0));
        Valid("AudioSource", AudioSourceDto.FromChannel(asset.Provenance.TrackId.Value, "drums.kick"));
        Valid("AudioSource", AudioSourceDto.FromAsset(asset.Id));
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var request = Schema("CreateAudioExportRequest")["properties"]!;
        var format = Schema("ExportFormat")["properties"]!;

        Schema("ChannelId")["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.ChannelIdPattern);
        Number(request["channels"]!["maxItems"]!).ShouldBe(ContractLimits.MaxExportChannels);
        Number(request["name"]!["maxLength"]!).ShouldBe(ContractLimits.AssetNameMaxLength);
        Number(Schema("Asset")["properties"]!["name"]!["maxLength"]!).ShouldBe(ContractLimits.AssetNameMaxLength);
        Strings(Schema("AudioFileFormat")["enum"]!).ShouldBe(ContractLimits.ExportFormats);
        Integers(format["bitDepth"]!["enum"]!).ShouldBe(ContractLimits.ExportBitDepths);
        Integers(format["mp3BitrateKbps"]!["enum"]!).ShouldBe(ContractLimits.Mp3Bitrates);
        Number(ListParameter("limit")["maximum"]!).ShouldBe(ContractLimits.MaxAssetPageSize);
        ContractLimits.MinExportRangeSeconds.ShouldBe(0.5);
        ContractLimits.MinFreeDiskBytesDefault.ShouldBe(1_073_741_824);
    }

    [Fact]
    public void Routes_AreContractPaths()
    {
        var id = Guid.Parse("3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e");
        var paths = OpenApiContract.Backend.Root["paths"]!.AsObject();

        ApiRoutes.Asset(id).ShouldBe($"/api/assets/{id}");
        ApiRoutes.RetryAsset(id).ShouldBe($"/api/assets/{id}/retry");
        ApiRoutes.AssetFile(id, ApiRoutes.AssetFileAudio).ShouldBe($"/api/assets/{id}/files/audio");
        ApiRoutes.AssetFile(id, ApiRoutes.AssetFileAudio, inline: true).ShouldBe($"/api/assets/{id}/files/audio?inline=true");
        foreach (var template in new[] { ApiRoutes.Assets, ApiRoutes.AudioExports, ApiRoutes.StemPacks, "/api/assets/{assetId}", "/api/assets/{assetId}/retry", "/api/assets/{assetId}/files/{role}" })
        {
            paths.ContainsKey(template).ShouldBeTrue(template);
        }
    }

    [Theory]
    [InlineData("render-queued-job", "QueuedRenderJob")]
    [InlineData("render-running-job", "RunningRenderJob")]
    [InlineData("render-completed-job", "CompletedRenderJob")]
    [InlineData("render-failed-job", "FailedRenderJob")]
    public void WorkerRenderExamples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        IsCopyOf(file, WorkerExample(example)).ShouldBeTrue(file);
    }

    [Theory]
    [InlineData("problem-400-input-path", "InvalidInputPath")]
    [InlineData("problem-404-input", "InputNotFound")]
    [InlineData("problem-409-render", "RenderNotCompleted")]
    [InlineData("problem-422-validation", "ValidationFailed")]
    public void WorkerProblemExamples_AreCopies(string file, string response)
    {
        var expected = WorkerContract.Root["components"]!["responses"]![response]!["content"]!["application/problem+json"]!["example"];

        IsCopyOf(file, expected).ShouldBeTrue(file);
    }

    [Fact]
    public void RenderVectors_UseContractChannelIdsAndAllowedLibraryPaths()
    {
        var path = new Regex($"^tracks/{TrackId}/(stems/[a-z]+|detail/[a-z]+/[a-z][a-z-]{{0,31}})\\.flac$", RegexOptions.None, TimeSpan.FromSeconds(1));
        var inputs = Vectors.Value["gate"]!.AsArray().SelectMany(c => c!["expected"]!["inputs"]?.AsArray() ?? []).ToList();

        inputs.ShouldNotBeEmpty();
        foreach (var input in inputs)
        {
            Regex.IsMatch(input!["channelId"]!.GetValue<string>(), ContractLimits.ChannelIdPattern, RegexOptions.None, TimeSpan.FromSeconds(1)).ShouldBeTrue();
            path.IsMatch(input["path"]!.GetValue<string>()).ShouldBeTrue();
        }
    }

    [Fact]
    public void RenderVectors_PanZeroIsTheIdentity()
    {
        var zero = Vectors.Value["pan"]!.AsArray().Single(p => p!["pan"]!.GetValue<double>() == 0)!;

        JsonNode.DeepEquals(zero["matrix"], JsonNode.Parse("[[1.0, 0.0], [0.0, 1.0]]")).ShouldBeTrue();
    }

    private static void Valid<T>(string schema, T body) =>
        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), JsonSerializer.SerializeToNode(body, Web)).ShouldBeEmpty(schema);

    private static JsonNode ListParameter(string name) =>
        OpenApiContract.Backend.Operation("listAssets")["parameters"]!.AsArray().Select(p => OpenApiContract.Backend.Resolve(p!))
            .Single(p => p["name"]!.GetValue<string>() == name)["schema"]!;

    private static IEnumerable<int> Integers(JsonNode array) => array.AsArray().Where(v => v is not null).Select(v => (int)v!.GetValue<double>());

    private static int Number(JsonNode node) => (int)node.GetValue<double>();
}
