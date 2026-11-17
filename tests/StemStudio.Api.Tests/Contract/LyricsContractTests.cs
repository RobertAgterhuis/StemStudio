using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, features 006 + 008 Phase 0, 006 T004): lyrics DTOs, routes and limits against backend-api 1.6.0
/// (feature 006), the worker 1.5.0 lyrics examples, and the rule 007 (1.5.0) -> 006 (1.6.0) -> 008 (1.7.0): every newer document
/// contains the older one unchanged except the named entries, so an owner working against either document builds one system.
/// </summary>
public sealed class LyricsContractTests
{
    private static readonly string[] TrackEntries = ["Track", "QueuedTrack", "SeparatingTrack", "CompletedTrack", "FailedTrack", "DetailQueuedTrack", "DetailCompletedTrack"];

    private static readonly string[] AssetEntries =
    [
        "AssetKind", "AssetFileRole", "AssetFile", "AssetOrigin", "Provenance", "Asset", "QueuedAsset", "RenderingAsset", "ReadyAsset", "FailedAsset",
        "/api/assets/{assetId}", "/api/assets/{assetId}/files/{role}",
    ];

    private static readonly string[] LyricsOperations = ["getLyrics", "setLyrics", "removeLyrics", "searchLyrics", "alignLyrics", "cancelLyricsAlignment", "setLyricsOffset"];

    [Fact]
    public void Contracts_HaveTheVersionsOfFeatures006And008()
    {
        Backend006.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.6.0");
        Backend008.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.7.0");
        // The linked worker document is 013's 1.7.0 since the 013 Phase 0 (it contains 1.6.0 unchanged).
        WorkerContract.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.8.0");
    }

    [Theory]
    [MemberData(nameof(Sections), MemberType = typeof(ContractJson))]
    public void NewerContracts_ContainTheOlderOnesUnchangedExceptTheNamedEntries(string section)
    {
        ChangedEntries(Backend007, Backend006, section).ShouldBeSubsetOf(TrackEntries);
        ChangedEntries(Backend006, Backend008, section).ShouldBeSubsetOf(AssetEntries);
    }

    [Fact]
    public void LyricsEnums_MatchTheContract()
    {
        var pairs = new (Type Enum, string Schema)[] { (typeof(LyricsStatusDto), "LyricsStatus"), (typeof(SyncLevelDto), "SyncLevel"), (typeof(WordOriginDto), "WordOrigin"), (typeof(LyricsOriginDto), "LyricsOrigin"), (typeof(LyricsSourceDto), "LyricsSource") };

        foreach (var (type, schema) in pairs)
        {
            WireNames(type).ShouldBe(Strings(Schema(schema)["enum"]!), schema);
        }
    }

    [Theory]
    [InlineData("FoundLyrics")]
    [InlineData("AlignPendingLyrics")]
    [InlineData("AligningLyrics")]
    [InlineData("NotFoundLyrics")]
    [InlineData("RemovedLyrics")]
    public void LyricsExamples_RoundTripThroughLyricsDto(string example)
    {
        var (expected, actual) = RoundTrip<LyricsDto>(example);

        JsonNode.DeepEquals(actual, expected).ShouldBeTrue(example);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Lyrics"), actual).ShouldBeEmpty();
    }

    [Fact]
    public void FoundLyrics_ExposeWordTimesAndTheCredit()
    {
        var dto = Example("FoundLyrics").Deserialize<LyricsDto>(Web)!;

        dto.Lines.Count.ShouldBe(2);
        dto.Lines[0].Words[2].ShouldBe(new LyricsWordDto("one", 11900, 13200, WordOriginDto.Source, LowConfidence: false));
        (dto.Credit, dto.SyncLevel, dto.Alignment).ShouldBe(("Lyrics: LRCLIB", SyncLevelDto.Word, (LyricsAlignmentDto?)null));
    }

    [Fact]
    public void AligningLyrics_CarryTheAlignmentProgressAndDevice()
    {
        var dto = Example("AligningLyrics").Deserialize<LyricsDto>(Web)!;

        dto.Alignment!.Progress.ShouldBe(40);
        dto.Alignment.Device!.Kind.ShouldBe(ProcessingDeviceKindDto.Gpu);
        dto.Lines.Single().StartMs.ShouldBeNull();
    }

    [Fact]
    public void TrackExamples_CarryTheLyricsStatus()
    {
        Example("CompletedTrack").Deserialize<TrackDto>(Web)!.LyricsStatus.ShouldBe(LyricsStatusDto.Found);
        Example("QueuedTrack").Deserialize<TrackDto>(Web)!.LyricsStatus.ShouldBe(LyricsStatusDto.None);
    }

    [Fact]
    public void TrackDto_SerializesLyricsStatusNoneByDefault()
    {
        var dto = Example("QueuedTrack").Deserialize<TrackDto>(Web)! with { LyricsStatus = default };

        var node = JsonSerializer.SerializeToNode(dto, Web)!;

        node["lyricsStatus"]!.GetValue<string>().ShouldBe("none");
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("Track"), node).ShouldBeEmpty();
        node.AsObject().ContainsKey("lines").ShouldBeFalse();
    }

    [Fact]
    public void Requests_MatchTheirSchemasAndLimits()
    {
        var text = Schema("SetLyricsRequest")["properties"]!;
        var offset = Schema("LyricsOffsetRequest")["properties"]!["offsetMs"]!;

        Valid("SetLyricsRequest", new SetLyricsRequest("Fake line one\nLa la la", "en"));
        Valid("SetLyricsRequest", new SetLyricsRequest("La"));
        Valid("LyricsOffsetRequest", new LyricsOffsetRequest(-120));
        Number(text["text"]!["maxLength"]!).ShouldBe(ContractLimits.MaxLyricsChars);
        text["language"]!["pattern"]!.GetValue<string>().ShouldBe(ContractLimits.LyricsLanguagePattern);
        (Number(offset["minimum"]!), Number(offset["maximum"]!), Number(offset["multipleOf"]!)).ShouldBe((-ContractLimits.MaxLyricsOffsetMs, ContractLimits.MaxLyricsOffsetMs, ContractLimits.LyricsOffsetStepMs));
        Number(Schema("Lyrics")["properties"]!["lines"]!["maxItems"]!).ShouldBe(ContractLimits.MaxLyricsLines);
    }

    [Fact]
    public void Routes_AreContractPaths()
    {
        var id = Guid.Parse("0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a");
        var paths = OpenApiContract.Backend.Root["paths"]!.AsObject();

        ApiRoutes.Lyrics(id).ShouldBe($"/api/tracks/{id}/lyrics");
        ApiRoutes.LyricsSearch(id).ShouldBe($"/api/tracks/{id}/lyrics/search");
        ApiRoutes.LyricsAlign(id).ShouldBe($"/api/tracks/{id}/lyrics/align");
        ApiRoutes.LyricsAlignCancel(id).ShouldBe($"/api/tracks/{id}/lyrics/align/cancel");
        ApiRoutes.LyricsOffset(id).ShouldBe($"/api/tracks/{id}/lyrics/offset");
        foreach (var suffix in new[] { "", "/search", "/align", "/align/cancel", "/offset" })
        {
            paths.ContainsKey($"/api/tracks/{{trackId}}/lyrics{suffix}").ShouldBeTrue(suffix);
        }
    }

    [Fact]
    public void EverySuccessfulLyricsResponse_IsNeverCached()
    {
        foreach (var operation in LyricsOperations)
        {
            var status = operation is "setLyrics" or "searchLyrics" or "alignLyrics" ? 202 : 200;
            var header = OpenApiContract.Backend.Response(operation, status)!["headers"]!["Cache-Control"]!;

            OpenApiContract.Backend.Resolve(header)["schema"]!["const"]!.GetValue<string>().ShouldBe("no-store", operation);
        }
    }

    [Theory]
    [InlineData("lyrics-lookup-request", "LookupLyricsRequest")]
    [InlineData("lyrics-align-request", "AlignLyricsRequest")]
    [InlineData("lyrics-queued-lookup-job", "QueuedLyricsLookupJob")]
    [InlineData("lyrics-queued-align-job", "QueuedLyricsAlignJob")]
    [InlineData("lyrics-running-align-job", "RunningLyricsAlignJob")]
    [InlineData("lyrics-completed-lookup-job", "CompletedLyricsLookupJob")]
    [InlineData("lyrics-failed-align-job", "FailedLyricsAlignJob")]
    [InlineData("lyrics-cancelled-align-job", "CancelledLyricsAlignJob")]
    [InlineData("lyrics-result-word", "WordSyncedLyricsResult")]
    [InlineData("lyrics-result-line", "LineSyncedLyricsResult")]
    [InlineData("lyrics-result-plain", "PlainLyricsResult")]
    [InlineData("lyrics-result-aligned", "AlignedLyricsResult")]
    [InlineData("lyrics-result-instrumental", "InstrumentalLyricsResult")]
    [InlineData("lyrics-result-not-found", "NotFoundLyricsResult")]
    [InlineData("lyrics-result-disabled", "DisabledLyricsResult")]
    public void WorkerLyricsExamples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        IsCopyOf(file, WorkerExample(example)).ShouldBeTrue(file);
    }

    [Fact]
    public void WorkerLyricsProblems_AreCopies()
    {
        var responses = WorkerContract.Root["components"]!["responses"]!;
        var conflict = responses["LyricsConflict"]!["content"]!["application/problem+json"]!["examples"]!["standardNotCompleted"]!["value"];
        var notCompleted = responses["LyricsNotCompleted"]!["content"]!["application/problem+json"]!["example"];

        IsCopyOf("problem-409-lyrics", conflict).ShouldBeTrue();
        IsCopyOf("problem-409-lyrics-result", notCompleted).ShouldBeTrue();
    }

    [Fact]
    public void WorkerHealthExamples_ReportTheLyricsAlignment()
    {
        var examples = WorkerContract.Root["paths"]!["/health"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!.AsObject();

        foreach (var example in examples)
        {
            example.Value!["value"]!["lyricsAlignment"]!["auto"]!.GetValue<string>().ShouldBe("gpu");
        }
    }

    private static void Valid<T>(string schema, T body) =>
        SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), JsonSerializer.SerializeToNode(body, Web)).ShouldBeEmpty(schema);

    private static int Number(JsonNode node) => (int)node.GetValue<double>();
}
