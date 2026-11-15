using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Scores;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 006 + 008 Phase 0, 008 T003): score DTOs, asset extensions, routes and limits against
/// backend-api 1.7.0 and the worker 1.6.0 transcription examples.
/// </summary>
public sealed class ScoreContractTests
{
    private static readonly (Type Enum, string Schema)[] EnumSchemas =
    [
        (typeof(ScoreProfileDto), "ScoreProfile"), (typeof(ScoreQualityDto), "ScoreQuality"), (typeof(ScoreResolutionDto), "ScoreResolution"),
        (typeof(GridStatusDto), "GridStatus"), (typeof(ScoreEngineDto), "ScoreEngine"), (typeof(ScoreWarningCodeDto), "ScoreWarningCode"),
        (typeof(AssetFileRoleDto), "AssetFileRole"), (typeof(AssetOriginDto), "AssetOrigin"),
    ];

    [Fact]
    public void ScoreEnums_MatchTheContract()
    {
        foreach (var (type, schema) in EnumSchemas)
        {
            WireNames(type).ShouldBe(Strings(Schema(schema)["enum"]!), schema);
        }
    }

    [Fact]
    public void KeyEnums_MatchTheKeyInfoSchema()
    {
        var key = Schema("KeyInfo")["properties"]!;

        WireNames(typeof(KeyModeDto)).ShouldBe(Strings(key["mode"]!["enum"]!));
        WireNames(typeof(KeySourceDto)).ShouldBe(Strings(key["source"]!["enum"]!));
    }

    [Theory]
    [InlineData("QueuedScoreAsset")]
    [InlineData("ReadyScoreAsset")]
    [InlineData("QueuedAsset")]
    [InlineData("ReadyAsset")]
    public void AssetExamples_RoundTripThroughAssetDto(string example)
    {
        var (expected, actual) = RoundTrip<AssetDto>(example, ignoreTimes: true);

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(example);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Asset"), JsonSerializer.SerializeToNode(Example(example).Deserialize<AssetDto>(Web), Web)).ShouldBeEmpty();
    }

    [Fact]
    public void ReadyScoreAsset_ExposesTheFactsTheRecipeAndTheThreeFiles()
    {
        var dto = Example("ReadyScoreAsset").Deserialize<AssetDto>(Web)!;

        dto.Kind.ShouldBe(AssetKindDto.Score);
        (dto.Score!.Key!.Tonic, dto.Score.Key.Mode, dto.Score.Bars, dto.Score.Pages).ShouldBe(("A-", KeyModeDto.Major, 125, 3));
        dto.Score.Warnings.Single().Code.ShouldBe(ScoreWarningCodeDto.NotesCleaned);
        (dto.Provenance.Origin, dto.Provenance.Recipe, dto.Provenance.ScoreRecipe!.Profile).ShouldBe((AssetOriginDto.Transcription, (ExportRecipeDto?)null, ScoreProfileDto.Bass));
        dto.Files.Select(f => f.Url).ShouldBe([
            ApiRoutes.AssetFile(dto.Id, ApiRoutes.AssetFilePdf),
            ApiRoutes.AssetFile(dto.Id, ApiRoutes.AssetFileMusicXml),
            ApiRoutes.AssetFile(dto.Id, ApiRoutes.AssetFileMidi),
        ]);
    }

    [Fact]
    public void ExportAssets_HaveNoScoreRecipe()
    {
        var dto = Example("ReadyAsset").Deserialize<AssetDto>(Web)!;

        (dto.Score, dto.Provenance.ScoreRecipe).ShouldBe(((ScoreFactsDto?)null, (ScoreRecipeDto?)null));
        dto.Provenance.Recipe.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("BassScoreRequest")]
    [InlineData("PianoScoreRequest")]
    public void RequestExamples_RoundTripThroughCreateScoreRequest(string example)
    {
        var json = Example(example);

        var again = JsonSerializer.SerializeToNode(json.Deserialize<CreateScoreRequest>(Web), Web);

        JsonNode.DeepEquals(again, json).ShouldBeTrue(example);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("CreateScoreRequest"), again).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("BassScoreOptions")]
    [InlineData("DrumsScoreOptions")]
    public void OptionsExamples_RoundTripThroughScoreOptionsDto(string example)
    {
        var json = Example(example);

        var again = JsonSerializer.SerializeToNode(json.Deserialize<ScoreOptionsDto>(Web), Web);

        JsonNode.DeepEquals(again, json).ShouldBeTrue(example);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("ScoreOptions"), again).ShouldBeEmpty();
    }

    [Fact]
    public void BassOptions_ListBothPianoEstimates()
    {
        var dto = Example("BassScoreOptions").Deserialize<ScoreOptionsDto>(Web)!;

        dto.Estimates.Where(e => e.Profile == ScoreProfileDto.Piano).Select(e => e.Quality).ShouldBe([ScoreQualityDto.Accurate, ScoreQualityDto.Fast]);
        (dto.GridStatus, dto.DefaultQuality, dto.SuggestedProfile).ShouldBe((GridStatusDto.Reliable, ScoreQualityDto.Fast, (ScoreProfileDto?)ScoreProfileDto.Bass));
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var tempo = Schema("ScoreTempo")["properties"]!;

        Strings(Schema("KeyName")["enum"]!).ShouldBe(ContractLimits.KeyNames);
        Strings(Schema("ScoreProfile")["enum"]!).ShouldBe(ContractLimits.ScoreProfiles);
        Strings(Schema("ScoreResolution")["enum"]!).ShouldBe(ContractLimits.ScoreResolutions);
        (tempo["bpm"]!["minimum"]!.GetValue<double>(), tempo["bpm"]!["maximum"]!.GetValue<double>()).ShouldBe((ContractLimits.MinBpm, ContractLimits.MaxBpm));
        (Number(tempo["beatsPerBar"]!["minimum"]!), Number(tempo["beatsPerBar"]!["maximum"]!)).ShouldBe((ContractLimits.MinBeatsPerBar, ContractLimits.MaxBeatsPerBar));
        Number(Schema("CreateScoreRequest")["properties"]!["name"]!["maxLength"]!).ShouldBe(ContractLimits.AssetNameMaxLength);
        ContractLimits.MinScoreRangeSeconds.ShouldBe(2);
        Strings(WorkerContract.Root["components"]!["schemas"]!["KeyName"]!["enum"]!).ShouldBe(ContractLimits.KeyNames);
    }

    [Fact]
    public void Routes_AreContractPaths()
    {
        var track = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
        var asset = Guid.Parse("3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e");
        var paths = OpenApiContract.Backend.Root["paths"]!.AsObject();

        paths.ContainsKey(ApiRoutes.Scores).ShouldBeTrue();
        paths.ContainsKey(ApiRoutes.ScoreOptions).ShouldBeTrue();
        ApiRoutes.ScoreOptionsForChannel(track, "vocals.lead").ShouldBe($"/api/scores/options?trackId={track}&channel=vocals.lead");
        ApiRoutes.ScoreOptionsForAsset(asset).ShouldBe($"/api/scores/options?assetId={asset}");
        ApiRoutes.AssetFile(asset, ApiRoutes.AssetFilePdf, inline: true).ShouldBe($"/api/assets/{asset}/files/pdf?inline=true");
        Strings(Schema("AssetFileRole")["enum"]!).ShouldBe([ApiRoutes.AssetFileAudio, ApiRoutes.AssetFilePdf, ApiRoutes.AssetFileMusicXml, ApiRoutes.AssetFileMidi]);
    }

    [Fact]
    public void AssetFile_ServesTheScoreMediaTypes()
    {
        var content = OpenApiContract.Backend.Response("getAssetFile", 200)!["content"]!.AsObject();

        content.Select(c => c.Key).ShouldBe(["audio/flac", "audio/wav", "audio/mpeg", "application/pdf", "application/vnd.recordare.musicxml+xml", "audio/midi"]);
        Strings(Schema("AssetFile")["properties"]!["contentType"]!["enum"]!).ShouldBe(content.Select(c => c.Key));
    }

    [Theory]
    [InlineData("transcription-request-bass", "BassTranscriptionRequest")]
    [InlineData("transcription-request-drums", "DrumsTranscriptionRequest")]
    [InlineData("transcription-request-piano", "PianoTranscriptionRequest")]
    [InlineData("transcription-queued-job", "QueuedTranscriptionJob")]
    [InlineData("transcription-running-job", "RunningTranscriptionJob")]
    [InlineData("transcription-completed-job", "CompletedTranscriptionJob")]
    [InlineData("transcription-failed-job", "FailedTranscriptionJob")]
    public void WorkerTranscriptionExamples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        IsCopyOf(file, WorkerExample(example)).ShouldBeTrue(file);
    }

    [Fact]
    public void WorkerTranscriptionProblem_IsACopy()
    {
        var expected = WorkerContract.Root["components"]!["responses"]!["TranscriptionNotCompleted"]!["content"]!["application/problem+json"]!["example"];

        IsCopyOf("problem-409-transcription", expected).ShouldBeTrue();
    }

    [Fact]
    public void WorkerSummary_MapsOntoScoreFactsDto()
    {
        var summary = WorkerExample("CompletedTranscriptionJob")["result"]!["summary"]!.DeepClone().AsObject();
        summary["device"] = null;

        var facts = summary.Deserialize<ScoreFactsDto>(Web)!;

        (facts.Engine, facts.Bpm, facts.TimeSignature, facts.Notes).ShouldBe((ScoreEngineDto.BasicPitch, 118, "4/4", 386));
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("ScoreFacts"), JsonSerializer.SerializeToNode(facts, Web)).ShouldBeEmpty();
    }

    private static int Number(JsonNode node) => (int)node.GetValue<double>();
}
