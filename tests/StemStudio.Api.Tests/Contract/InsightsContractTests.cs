using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.Handouts;
using StemStudio.Contracts.Insights;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 020 Phase 0, T002/T003): backend-api 1.19.0 (<c>specs/020-insights-handouts/contracts/backend-api.openapi.yaml</c>)
/// is 016's 1.15.0 plus exactly the 020 delta without its deferred G2 parts (DELTA.md 3 and "Phase 0 outcome"), and the Insights DTOs and
/// routes match it. The handout DTOs are covered by <see cref="HandoutContractTests"/>.
/// </summary>
public sealed class InsightsContractTests
{
    internal static readonly string[] Operations =
        ["getQualityReport", "requestQualityReport", "getHandoutOptions", "createHandout", "getPracticeNotes", "setPracticeNote"];

    private static readonly string[] DeferredOperations = ["getReseparationOptions", "startReseparation", "cancelReseparation"];

    private static readonly string[] SharedCopies = ["ChannelId", "StemName", "ParentStemName", "Asset", "ProblemDetails", "ValidationProblemDetails"];

    private static readonly string[] DeferredSchemas =
        ["SeparationPipeline", "StemSet", "ReseparationStatus", "ReseparationPhase", "ReseparationOptions", "StartReseparationRequest"];

    public static TheoryData<string, string[]> Changed1150Entries => new()
    {
        { "paths", [] },
        { "components/schemas", ["Track", "Asset", "Provenance", "AssetKind", "AssetOrigin"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string, object> SampleInstances => new()
    {
        { "QualityBadge", new QualityBadgeDto { Status = QualityReportStatusDto.Running, Summary = Summary(), Outdated = true } },
        { "QualityReport", EmptyReport() },
        { "Tip", new TipDto { Code = TipCodeDto.HardToIsolate, Text = "Guitar is hard to isolate.", Action = null } },
        {
            "TipAction", new TipActionDto { Kind = TipActionKindDto.OpenDetail, Channel = "drums", Parent = "drums", Child = "rest" }
        },
    };

    [Fact]
    public void Backend_IsLabelled1190AndListsTheMergedDeltas()
    {
        var info = OpenApiContract.Backend.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.19.0");
        Strings(info["x-stemstudio-contains"]!).TakeLast(2).ShouldBe(["1.15.0", "1.19.0"]);
        Backend020Delta.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.19.0-delta.3");
    }

    [Theory]
    [MemberData(nameof(Changed1150Entries))]
    public void Backend1190_Contains1150UnchangedExceptTheAdditions(string section, string[] changed)
    {
        ChangedEntries(Backend016, OpenApiContract.Backend, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Fact]
    public void Backend1190_AddsExactlyTheSixOperationsAndNoDeferredOne()
    {
        var added = OpenApiContract.Backend.Operations.Select(OperationOf).Except(Backend016.Operations.Select(OperationOf)).ToList();

        added.ShouldBe(Operations, ignoreOrder: true);
        Backend020Delta.Operations.Select(OperationOf).ShouldBe([.. Operations, .. DeferredOperations], ignoreOrder: true);
        OpenApiContract.Backend.Operations.ShouldNotContain(op => op.Path.Contains("reseparation", StringComparison.Ordinal));
    }

    [Fact]
    public void InsightsOperations_CarryNoWriteGate()
    {
        // Database edits, job starts and new assets only (013 data-model 5.2 "Allowed"); maintenance (503) is the middleware's.
        var added = OpenApiContract.Backend.Operations.Where(op => Operations.Contains(OperationOf(op))).ToList();

        added.Count.ShouldBe(Operations.Length);
        added.ShouldAllBe(op => op.Operation["x-stemstudio-write-gate"] == null);
        Backend020Delta.Operations.ShouldAllBe(op => op.Operation["x-stemstudio-write-gate"] == null);
    }

    [Fact]
    public void DeltaSchemas_AreMergedUnchanged_SharedCopiesAndDeferredOnesAreNot()
    {
        var merged = Section(OpenApiContract.Backend.Root, "components/schemas");
        var delta = Section(Backend020Delta.Root, "components/schemas");
        var changed = delta.Where(s => !SharedCopies.Contains(s.Key) && !DeferredSchemas.Contains(s.Key))
            .Where(s => !JsonNode.DeepEquals(s.Value, merged[s.Key]))
            .Select(s => s.Key);

        changed.ShouldBeEmpty();
        DeferredSchemas.ShouldAllBe(name => !merged.ContainsKey(name));
        Section(OpenApiContract.Backend.Root, "components/responses").ContainsKey("ReseparationConflict").ShouldBeFalse();
    }

    [Fact]
    public void Extensions_AreOptionalPropertiesOfTheBaseSchemas()
    {
        var merged = Section(OpenApiContract.Backend.Root, "components/schemas");
        var expected = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Track"] = "quality",
            ["Asset"] = "handout",
            ["Provenance"] = "handoutRecipe",
        };
        foreach (var (schema, property) in expected)
        {
            merged[schema]!["properties"]!.AsObject().ContainsKey(property).ShouldBeTrue(schema);
            Strings(merged[schema]!["required"]!).ShouldNotContain(property, schema);
        }

        Strings(merged["AssetKind"]!["enum"]!).Last().ShouldBe("handout");
        Strings(merged["AssetOrigin"]!["enum"]!).Last().ShouldBe("handout");
    }

    [Fact]
    public void ReadyQualityReportExample_RoundTripsAndValidates()
    {
        var json = Example("ReadyQualityReport");
        var again = JsonSerializer.SerializeToNode(json.Deserialize<QualityReportDto>(Web), Web)!;

        JsonNode.DeepEquals(WithoutMeasuredAt(json), WithoutMeasuredAt(again)).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("QualityReport"), again).ShouldBeEmpty();
        json.Deserialize<QualityReportDto>(Web)!.MeasuredAt.ShouldBe(new DateTimeOffset(2026, 11, 16, 10, 0, 15, TimeSpan.Zero));
    }

    [Theory]
    [MemberData(nameof(SampleInstances))]
    public void ConstructedDtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        var node = JsonSerializer.SerializeToNode(dto, dto.GetType(), Web);

        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), node).ShouldBeEmpty();
    }

    [Fact]
    public void TrackWithoutAReport_SerializesWithoutQuality()
    {
        var track = JsonSerializer.SerializeToNode(SampleTrack(), Web)!.AsObject();

        track.ContainsKey("quality").ShouldBeFalse();
    }

    [Fact]
    public void TrackWithABadge_WritesAValidQualityBadge()
    {
        var dto = SampleTrack() with
        {
            Quality = new QualityBadgeDto { Status = QualityReportStatusDto.Ready, Summary = Summary(), Outdated = false },
        };
        var quality = JsonSerializer.SerializeToNode(dto, Web)!["quality"]!;

        quality["status"]!.GetValue<string>().ShouldBe("ready");
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("QualityBadge"), quality).ShouldBeEmpty();
    }

    [Fact]
    public void ComponentEnums_MatchTheirSchemas()
    {
        AssertEnumsMatch([
            (typeof(QualityReportStatusDto), "QualityReportStatus"), (typeof(QualityLevelDto), "QualityLevel"),
            (typeof(ReliabilityDto), "Reliability"), (typeof(QualityRatingDto), "QualityRating"), (typeof(TipCodeDto), "TipCode"),
            (typeof(TipActionKindDto), "TipActionKind"),
        ]);
        WireNames(typeof(ArtefactKindDto)).ShouldBe(Strings(Schema("ArtefactView")["properties"]!["kinds"]!["items"]!["enum"]!));
    }

    [Fact]
    public void TipCodes_HaveNoGpuOrDeferredTip()
    {
        WireNames(typeof(TipCodeDto)).ShouldNotContain("rerun-high-quality");
        WireNames(typeof(TipActionKindDto)).ShouldNotContain("reseparate");
        WireNames(typeof(TipCodeDto)).ShouldAllBe(code => !code.Contains("gpu", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void LimitsAndTexts_MatchTheSchemas()
    {
        Schema("QualityReport")["properties"]!["disclaimer"]!["const"]!.GetValue<string>().ShouldBe(ContractLimits.QualityDisclaimer);
        Schema("StemQualityView")["properties"]!["tips"]!["maxItems"]!.GetValue<double>().ShouldBe(ContractLimits.MaxTipsPerStem);
        Schema("PracticeNote")["properties"]!["text"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.MaxPracticeNoteLength);
        Schema("SetPracticeNoteRequest")["properties"]!["text"]!["maxLength"]!.GetValue<double>().ShouldBe(ContractLimits.MaxPracticeNoteLength);
        Schema("HandoutFacts")["properties"]!["songs"]!["maximum"]!.GetValue<double>().ShouldBe(ContractLimits.MaxSetlistHandoutSongs);
    }

    [Fact]
    public void NoticeTexts_AreTheWorkerExampleTexts()
    {
        var notice = WorkerExample("BassHandoutRequest")["document"]!["notice"]!;

        notice["footer"]!.GetValue<string>().ShouldBe(ContractLimits.HandoutNoticeFooter);
        notice["full"]!.GetValue<string>().ShouldBe(ContractLimits.HandoutNoticeFull);
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchContractPaths(string route, string method)
    {
        OpenApiContract.Backend.Operations.ShouldContain(op => op.Method == method && PathTemplate(op.Path).IsMatch(route), route);
    }

    [Fact]
    public void HandoutOptionsRoute_WritesTheWireValues()
    {
        var id = Guid.Parse("7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f");

        ApiRoutes.HandoutOptions(HandoutSourceDto.Song(id), HandoutFamilyDto.Melodic, TranspositionDto.BFlat)
            .ShouldBe($"/api/handouts/options?trackId={id}&family=melodic&transposition=b-flat");
        ApiRoutes.HandoutOptions(HandoutSourceDto.Setlist(id), HandoutFamilyDto.Overview)
            .ShouldBe($"/api/handouts/options?setlistId={id}&family=overview");
        ApiRoutes.PracticeNote(id, HandoutFamilyDto.Keys).ShouldBe($"/api/tracks/{id}/practice-notes/keys");
    }

    public static TheoryData<string, string> Routes()
    {
        var id = Guid.NewGuid();
        return new()
        {
            { ApiRoutes.Quality(id), "get" }, { ApiRoutes.Quality(id), "post" },
            { ApiRoutes.HandoutOptions(HandoutSourceDto.Song(id), HandoutFamilyDto.Bass), "get" }, { ApiRoutes.Handouts, "post" },
            { ApiRoutes.PracticeNotes(id), "get" }, { ApiRoutes.PracticeNote(id, HandoutFamilyDto.Drums), "put" },
        };
    }

    internal static QualitySummaryDto Summary() => new() { Good = 3, Fair = 2, Poor = 0, NotPresent = 1, Unknown = 0 };

    private static QualityReportDto EmptyReport() => new()
    {
        Status = QualityReportStatusDto.None,
        Progress = 0,
        Error = null,
        AnalyzerVersion = null,
        MeasuredAt = null,
        Outdated = false,
        Disclaimer = ContractLimits.QualityDisclaimer,
        Summary = new() { Good = 0, Fair = 0, Poor = 0, NotPresent = 0, Unknown = 0 },
        Stems = [],
    };

    private static TrackDto SampleTrack() => new()
    {
        Id = Guid.Empty,
        Title = "Song",
        SourceType = SourceTypeDto.Youtube,
        SourceKey = "youtube:dQw4w9WgXcQ",
        Status = TrackStatusDto.Completed,
        Progress = 100,
        Stems = [],
        CreatedAt = DateTimeOffset.UnixEpoch,
        UpdatedAt = DateTimeOffset.UnixEpoch,
    };

    private static JsonObject WithoutMeasuredAt(JsonNode node)
    {
        var copy = node.DeepClone().AsObject();
        copy.Remove("measuredAt");
        return copy;
    }

    private static string OperationOf((string Path, string Method, JsonObject Operation) op) => OperationId(op.Operation);

    private static Regex PathTemplate(string path) =>
        new("^" + Regex.Replace(path, @"\{[^}]+\}", "[^/?]+") + @"(\?.*)?$", RegexOptions.None, TimeSpan.FromSeconds(1));
}
