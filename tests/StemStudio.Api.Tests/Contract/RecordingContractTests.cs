using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 015 Phase 0, T002/T003): backend-api 1.14.0 (<c>specs/015-play-along-recording/contracts/backend-api.openapi.yaml</c>)
/// is 014's 1.13.0 (re-based at the 014+015 round start, DELTA.md "Re-merge"; 013's 1.12.0 in Phase 0) plus exactly the 015 delta (DELTA.md 1-2 and "Phase 0 outcome"), and the Recording DTOs, routes and limits match it.
/// </summary>
public sealed partial class RecordingContractTests
{
    private static readonly string[] RecordingOperations =
    [
        "listRecordingTakes", "createRecordingTake", "deleteRecordingTakes", "getRecordingTake", "updateRecordingTake", "deleteRecordingTake",
        "replaceRecordingTakeAudio", "retryRecordingTake", "listInputCalibrations", "upsertInputCalibration", "deleteInputCalibration",
    ];

    private static readonly string[] GatedOperations = ["deleteRecordingTakes", "deleteRecordingTake", "replaceRecordingTakeAudio"];

    public static TheoryData<string, string[]> Changed1130Entries => new()
    {
        { "paths", ["/api/assets", "/api/assets/{assetId}", "/api/assets/{assetId}/retry"] },
        { "components/schemas", ["AssetOrigin", "Provenance", "CreateAudioExportRequest", "ExportRecipe", "ExportMix", "MixSettings", "Track"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string, Type> Examples => new()
    {
        { "SavingTakeExample", typeof(RecordingTakeDto) },
        { "ReadyTakeExample", typeof(RecordingTakeDto) },
        { "TakeListExample", typeof(RecordingTakeListDto) },
    };

    public static TheoryData<string, object> SampleInstances => new()
    {
        { "RecordingTakeUpload", Upload() },
        { "ReplaceTakeAudioMetadata", new ReplaceTakeAudioMetadataDto { StartSample = 0, LengthSamples = RecordingLimits.MinTakeSamples, AdjustmentSamples = -441 } },
        { "UpdateTakeRequest", new UpdateTakeRequest { Favorite = true } },
        { "DeleteTakesRequest", new DeleteTakesRequest { TakeIds = [Guid.NewGuid()] } },
        { "DeleteTakesResult", new DeleteTakesResultDto { Deleted = 3 } },
        { "InputCalibrationInput", Calibration() },
        { "InputCalibration", CalibrationRead() },
        { "TakeChannelSettings", Channel() },
        { "RecordingRecipe", new RecordingRecipeDto { TakeId = Guid.NewGuid() } },
    };

    [Fact]
    public void Backend_IsLabelled1140AndListsTheMergedDeltas()
    {
        var info = OpenApiContract.Backend.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.14.0");
        Strings(info["x-stemstudio-contains"]!).ShouldBe(["1.1.0", "1.2.0", "1.3.0", "1.4.0", "1.5.0", "1.6.0", "1.7.0", "1.8.0", "1.9.0", "1.10.0", "1.21.0", "1.11.0", "1.12.0", "1.22.0", "1.23.0", "1.13.0", "1.14.0"]);
    }

    [Theory]
    [MemberData(nameof(Changed1130Entries))]
    public void Backend1140_Contains1130UnchangedExceptTheAdditions(string section, string[] changed)
    {
        ChangedEntries(Backend014, OpenApiContract.Backend, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Fact]
    public void Backend1140_AddsExactlyTheElevenRecordingOperations()
    {
        var added = OpenApiContract.Backend.Operations.Select(Id).Except(Backend014.Operations.Select(Id)).ToList();

        added.ShouldBe(RecordingOperations, ignoreOrder: true);
        Backend015Delta.Operations.Select(Id).ShouldBe(RecordingOperations, ignoreOrder: true);
    }

    [Fact]
    public void DeltaSchemas_AreMergedUnchanged()
    {
        var merged = Section(OpenApiContract.Backend.Root, "components/schemas");
        var delta = Section(Backend015Delta.Root, "components/schemas")
            .Where(s => !s.Key.EndsWith("Additions", StringComparison.Ordinal) && s.Key is not ("ProblemDetails" or "ValidationProblemDetails"));

        delta.Where(s => !JsonNode.DeepEquals(s.Value, merged[s.Key])).Select(s => s.Key).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(RecordingOperationIds))]
    public void DeltaOperations_KeepPathMethodAndRequestBody(string operationId)
    {
        var delta = Backend015Delta.Operations.Single(op => Id(op) == operationId);
        var merged = OpenApiContract.Backend.Operations.Single(op => Id(op) == operationId);

        (merged.Path, merged.Method).ShouldBe((delta.Path, delta.Method));
        JsonNode.DeepEquals(delta.Operation["requestBody"], merged.Operation["requestBody"]).ShouldBeTrue(operationId);
        Codes(delta.Operation).Except(Codes(merged.Operation)).ShouldBeEmpty(operationId);
    }

    public static TheoryData<string> RecordingOperationIds => new(RecordingOperations);

    [Fact]
    public void TakeOperationsThatDeleteOrReplaceFiles_CarryTheWriteGate()
    {
        foreach (var op in OpenApiContract.Backend.Operations.Where(op => RecordingOperations.Contains(Id(op))))
        {
            var gated = GatedOperations.Contains(Id(op));

            (op.Operation["x-stemstudio-write-gate"]?.GetValue<string>()).ShouldBe(gated ? "backupRunning" : null, Id(op));
            if (gated)
            {
                Codes(op.Operation).ShouldContain("409");
                Codes(op.Operation).ShouldContain("503");
            }
        }
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_RoundTripsAndValidates(string example, Type type)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize(type, Web), type, Web)!;

        JsonNode.DeepEquals(Canonical(json), Canonical(again)).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(OpenApiContract.Backend, Schema(SchemaOf(type)), again).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(SampleInstances))]
    public void ConstructedDtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var node = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), node).ShouldBeEmpty();
    }

    [Fact]
    public void ComponentEnums_MatchTheirSchemas()
    {
        AssertEnumsMatch([
            (typeof(RecordingPartDto), "RecordingPart"), (typeof(InputChannelModeDto), "InputChannelMode"), (typeof(LatencyMethodDto), "LatencyMethod"),
            (typeof(CalibrationMethodDto), "CalibrationMethod"), (typeof(TakeStatusDto), "TakeStatus"), (typeof(AssetOriginDto), "AssetOrigin"),
        ]);
    }

    [Fact]
    public void Additions_AreOptionalAndAbsentWhenUnused()
    {
        var mix = JsonSerializer.SerializeToNode(new MixSettingsDto("Mix", 1, [], new MetronomeSettingsDto(false, 0.5, null, 0, 4), null), Web)!.AsObject();
        var export = JsonSerializer.SerializeToNode(new CreateAudioExportRequest { TrackId = Guid.NewGuid(), Mode = ExportModeDto.Raw, Format = new ExportFormatDto(AudioFileFormatDto.Flac) }, Web)!.AsObject();
        var provenance = new ProvenanceDto { Origin = AssetOriginDto.Recording, TrackTitle = "Song", TrackAvailable = true, Contents = "Take 1 - Bass (recording)" };

        (mix.ContainsKey("take"), export.ContainsKey("takeId")).ShouldBe((false, false));
        JsonSerializer.SerializeToNode(provenance, Web)!.AsObject().ContainsKey("recording").ShouldBeFalse();
        Strings(Schema("MixSettings")["required"]!).ShouldNotContain("take");
        Strings(Schema("Track")["required"]!).ShouldNotContain("takeCount");
        Strings(Schema("Provenance")["required"]!).ShouldNotContain("recording");
        JsonSerializer.Deserialize<TrackDto>(TrackJsonWithoutTakeCount(), Web)!.TakeCount.ShouldBe(0);
    }

    [Fact]
    public void TakeAdditions_RoundTripAndValidate()
    {
        var mix = new MixSettingsDto("Mix", 1, [], new MetronomeSettingsDto(false, 0.5, null, 0, 4), null) { Take = Channel() };
        var provenance = new ProvenanceDto
        {
            Origin = AssetOriginDto.Recording,
            TrackTitle = "Song",
            TrackAvailable = true,
            Contents = "Take 1 - Bass (recording)",
            Recording = new RecordingRecipeDto { TakeId = Guid.NewGuid() },
        };
        var mixNode = JsonSerializer.SerializeToNode(mix, Web)!;
        var provenanceNode = JsonSerializer.SerializeToNode(provenance, Web)!;

        mixNode.Deserialize<MixSettingsDto>(Web)!.Take.ShouldBe(mix.Take);
        provenanceNode["recording"]!["kind"]!.GetValue<string>().ShouldBe("recording");
        provenanceNode["recording"]!["captureFile"]!.GetValue<string>().ShouldBe("capture.wav");
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("MixSettings"), mixNode).ShouldBeEmpty();
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Provenance"), provenanceNode).ShouldBeEmpty();
    }

    [Fact]
    public void UpdateTakeRequest_SendsOnlyTheGivenFields()
    {
        JsonSerializer.Serialize(new UpdateTakeRequest { Part = RecordingPartDto.Piano }, Web).ShouldBe("""{"part":"piano"}""");
        Int(Schema("UpdateTakeRequest"), "minProperties").ShouldBe(1);
    }

    [Fact]
    public void ChannelPatterns_MatchTheContract()
    {
        Schema("ChannelId")["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.ChannelIdPattern);
        Schema("ExportChannelId")["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.ExportChannelIdPattern);
        Schema("CreateAudioExportRequest")["properties"]!["channels"]!["items"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/ExportChannelId");
        Schema("ExportRecipe")["properties"]!["channels"]!["items"]!["$ref"]!.GetValue<string>().ShouldBe("#/components/schemas/ExportChannelId");
        ExportChannel().IsMatch("take").ShouldBeTrue();
        new[] { "vocals", "drums.kick", "other.acoustic-guitar" }.ShouldAllBe(id => ExportChannel().IsMatch(id) && StemChannel().IsMatch(id));
        new[] { "takes", "take.x", "Take", "kick" }.ShouldAllBe(id => !ExportChannel().IsMatch(id));
        StemChannel().IsMatch("take").ShouldBeFalse();
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var upload = Schema("RecordingTakeUpload")["properties"]!;
        var input = Schema("InputCalibrationInput")["properties"]!;

        (Long(upload["lengthSamples"]!, "minimum"), Int(upload["latencySamples"]!, "maximum"), Int(upload["passIndex"]!, "maximum"))
            .ShouldBe((RecordingLimits.MinTakeSamples, RecordingLimits.MaxLatencySamples, RecordingLimits.MaxPassesPerSession));
        (Int(upload["name"]!, "maxLength"), Int(upload["deviceLabel"]!, "maxLength")).ShouldBe((RecordingLimits.MaxTakeNameLength, RecordingLimits.MaxDeviceLabelLength));
        Int(Schema("ReplaceTakeAudioMetadata")["properties"]!["adjustmentSamples"]!, "maximum").ShouldBe(RecordingLimits.MaxAdjustmentSamples);
        Int(Schema("ReplaceTakeAudioMetadata")["properties"]!["adjustmentSamples"]!, "minimum").ShouldBe(-RecordingLimits.MaxAdjustmentSamples);
        (Int(input["spreadSamples"]!, "maximum"), Int(input["detectedClicks"]!, "minimum"), Int(input["detectedClicks"]!, "maximum"))
            .ShouldBe((RecordingLimits.MaxSpreadSamples, RecordingLimits.MinDetectedClicks, RecordingLimits.MaxDetectedClicks));
        Int(input["deviceIdHint"]!, "maxLength").ShouldBe(RecordingLimits.MaxDeviceIdHintLength);
        (Int(Schema("RecordingTakeList")["properties"]!["items"]!, "maxItems"), Int(Schema("Track")["properties"]!["takeCount"]!, "maximum"))
            .ShouldBe((RecordingLimits.MaxTakesPerTrack, RecordingLimits.MaxTakesPerTrack));
        Int(Schema("RecordingTake")["properties"]!["error"]!, "maxLength").ShouldBe(RecordingLimits.MaxErrorLength);
        Int(Schema("DeleteTakesRequest")["properties"]!["takeIds"]!, "maxItems").ShouldBe(RecordingLimits.MaxTakesPerTrack);
    }

    [Theory]
    [InlineData(0.0, 0L)]
    [InlineData(10.0, 441L)]
    [InlineData(-10.0, -441L)]
    [InlineData(0.1, 4L)]
    [InlineData(25.0, 1103L)]
    [InlineData(1000.0, 44_100L)]
    [InlineData(-200.0, -8_820L)]
    public void ToSamples_FollowsTheRoundingRule(double milliseconds, long samples)
    {
        RecordingLimits.ToSamples(milliseconds).ShouldBe(samples);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchContractPaths(string route, string method)
    {
        OpenApiContract.Backend.Operations.ShouldContain(op => op.Method == method && Template(op.Path).IsMatch(route), route);
    }

    public static TheoryData<string, string> Routes()
    {
        var id = Guid.NewGuid();
        return new()
        {
            { ApiRoutes.TrackTakesOf(id), "get" }, { ApiRoutes.TrackTakesOf(id), "post" }, { ApiRoutes.TrackTakesDeleteOf(id), "post" },
            { ApiRoutes.RecordingTakeOf(id), "get" }, { ApiRoutes.RecordingTakeOf(id), "patch" }, { ApiRoutes.RecordingTakeOf(id), "delete" },
            { ApiRoutes.RecordingTakeAudioOf(id), "put" }, { ApiRoutes.RecordingTakeRetryOf(id), "post" }, { ApiRoutes.Calibrations, "get" },
            { ApiRoutes.Calibrations, "put" }, { ApiRoutes.CalibrationOf(id), "delete" },
        };
    }

    [Fact]
    public void RouteTemplates_AreTheContractPaths()
    {
        var paths = OpenApiContract.Backend.Root["paths"]!.AsObject().Select(p => p.Key).ToHashSet(StringComparer.Ordinal);

        new[] { ApiRoutes.TrackTakes, ApiRoutes.TrackTakesDelete, ApiRoutes.RecordingTake, ApiRoutes.RecordingTakeAudio, ApiRoutes.RecordingTakeRetry, ApiRoutes.Calibrations, ApiRoutes.Calibration }
            .ShouldAllBe(route => paths.Contains(route));
        var form = Schema("RecordingTakeUploadForm")["properties"]!.AsObject().Select(p => p.Key);
        form.ShouldBe([ApiRoutes.RecordingTakeAudioPart, ApiRoutes.RecordingTakeMetadataPart]);
    }

    [Fact]
    public void ProblemTypes_AreTheContractCodes()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "backend-api.openapi.yaml"));
        var codes = typeof(RecordingProblemTypes).GetFields().Select(f => (string)f.GetValue(null)!).ToList();

        codes.Count.ShouldBe(14);
        codes.ShouldAllBe(code => text.Contains($"`{code}`", StringComparison.Ordinal));
    }

    private static RecordingTakeUploadDto Upload() => new()
    {
        SessionId = Guid.NewGuid(),
        Partial = false,
        StartSample = 0,
        LengthSamples = 44_100,
        InputMode = InputChannelModeDto.Stereo,
        LatencySamples = 1_102,
        LatencyMethod = LatencyMethodDto.Estimated,
        Part = RecordingPartDto.Drums,
        Speed = 1,
        TransposeSemitones = 0,
        Dropouts = 0,
        RecordedAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
    };

    private static InputCalibrationInput Calibration() => new()
    {
        DeviceLabel = "USB interface",
        LatencySamples = 1_102,
        Method = CalibrationMethodDto.Loopback,
        SpreadSamples = 12,
        DetectedClicks = 8,
        OutputLatencySamples = 441,
    };

    private static InputCalibrationDto CalibrationRead() => new()
    {
        Id = Guid.NewGuid(),
        DeviceLabel = "USB interface",
        LatencySamples = 1_102,
        LatencyMilliseconds = 24.99,
        Method = CalibrationMethodDto.Manual,
        OutputLatencySamples = 441,
        MeasuredAt = new DateTimeOffset(2026, 11, 17, 9, 0, 0, TimeSpan.Zero),
    };

    private static TakeChannelSettingsDto Channel() => new() { TakeId = Guid.NewGuid(), Volume = 1, Pan = 0, Mute = false, Solo = true };

    private static string TrackJsonWithoutTakeCount() =>
        """{"id":"0b7e4f1c-6a2d-4c8e-9f10-2a3b4c5d6e7f","title":"Song","sourceType":"upload","sourceKey":"upload:abc","status":"completed","progress":100,"stems":[],"createdAt":"2026-11-17T09:00:00Z","updatedAt":"2026-11-17T09:00:00Z"}""";

    private static string Id((string Path, string Method, JsonObject Operation) op) => op.Operation["operationId"]!.GetValue<string>();

    private static List<string> Codes(JsonObject operation) => [.. operation["responses"]!.AsObject().Select(r => r.Key)];

    private static int Int(JsonNode node, string member) => int.Parse(node[member]!.ToString(), CultureInfo.InvariantCulture);

    private static long Long(JsonNode node, string member) => long.Parse(node[member]!.ToString(), CultureInfo.InvariantCulture);

    private static string SchemaOf(Type type) => type.Name.Replace("Dto", string.Empty, StringComparison.Ordinal);

    private static Regex Template(string path) =>
        new("^" + Regex.Replace(Regex.Escape(path), @"\\\{[A-Za-z]+\}", "[^/]+") + "$", RegexOptions.None, TimeSpan.FromSeconds(1));

    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Canonical)]),
        JsonValue value when value.TryGetValue<string>(out var text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            && text.Contains('T', StringComparison.Ordinal) => JsonValue.Create(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        _ => node?.DeepClone(),
    };

    [GeneratedRegex(ContractLimits.ExportChannelIdPattern)]
    private static partial Regex ExportChannel();

    [GeneratedRegex(ContractLimits.ChannelIdPattern)]
    private static partial Regex StemChannel();
}
