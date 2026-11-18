using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Api.SongSections;
using StemStudio.Contracts;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Practice;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Sections;
using StemStudio.Contracts.Setlists;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 014 Phase 0 part 2, T003/T005): backend-api 1.13.0 (<c>specs/014-practice-flow/contracts/backend-api.openapi.yaml</c>)
/// is 023's 1.23.0 (re-based at the 014+015 round start, DELTA.md 8; 013's 1.12.0 in Phase 0) plus exactly the 014 delta
/// (1.13.0-delta.2), worker-api 1.8.0 is 1.7.0 plus the practice lane, and the
/// Sections, ScoreFollow, Setlists and Practice DTOs, routes, limits and problem types match them.
/// </summary>
public sealed class PracticeFlowContractTests
{
    // Delta components that already exist in 1.12.0 (kept there); LoopRegion is the one changed schema (sectionId).
    private static readonly HashSet<string> SharedComponents =
    [
        "schemas/ChannelId", "schemas/ScoreProfile", "schemas/ProblemDetails", "schemas/ValidationProblemDetails",
        "responses/NotFound", "responses/ValidationProblem", "responses/WorkerUnavailable", "parameters/AssetId", "parameters/TrackId",
    ];

    // The full document 1.13.0 was merged onto (merge_full.py of 014); every assertion below proves "base + exactly the 014 delta".
    private static OpenApiContract BaseDocument => Backend023;

    // The 1.13.0 document under test (linked as Contract/014); the current full document is 015's 1.14.0 on top of it.
    private static OpenApiContract Backend => Backend014;

    public static TheoryData<string, string[]> Changed1120Entries => new()
    {
        { "paths", [] },
        { "components/schemas", ["LoopRegion"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string, Type> Examples => new()
    {
        { "SectionSetReady", typeof(SectionSetDto) },
        { "ScoreViewReady", typeof(ScoreViewDto) },
        { "SetlistRehearsal", typeof(SetlistDto) },
        { "PracticeSummaryWeek", typeof(PracticeSummaryDto) },
    };

    public static TheoryData<Type, string> DtoSchemas => new()
    {
        { typeof(SectionWarningDto), "SectionWarning" }, { typeof(SectionDto), "Section" }, { typeof(SectionSetDto), "SectionSet" },
        { typeof(SectionInputDto), "SectionInput" }, { typeof(SectionsUpdateRequest), "SectionsUpdate" },
        { typeof(DetectSectionsRequest), "DetectSectionsRequest" }, { typeof(TrackScoreDto), "TrackScore" },
        { typeof(ScoreViewPageDto), "ScoreViewPage" }, { typeof(ScoreBarDto), "ScoreBar" }, { typeof(ScoreViewDto), "ScoreView" },
        { typeof(SetlistCreateRequest), "SetlistCreate" }, { typeof(SetlistUpdateRequest), "SetlistUpdate" },
        { typeof(SetlistEntryInputDto), "SetlistEntryInput" }, { typeof(SetlistEntriesUpdateRequest), "SetlistEntriesUpdate" },
        { typeof(SetlistEntryDto), "SetlistEntry" }, { typeof(SetlistDto), "Setlist" }, { typeof(SetlistSummaryDto), "SetlistSummary" },
        { typeof(PracticeLogSettingsDto), "PracticeLogSettings" }, { typeof(PracticeLogSettingsUpdateRequest), "PracticeLogSettingsUpdate" },
        { typeof(PracticeSectionInputDto), "PracticeSectionInput" }, { typeof(PracticeSessionInputDto), "PracticeSessionInput" },
        { typeof(PracticeSessionDto), "PracticeSession" }, { typeof(PracticeDayDto), "PracticeDay" },
        { typeof(PracticeSectionTotalDto), "PracticeSectionTotal" }, { typeof(PracticeTempoPointDto), "PracticeTempoPoint" },
        { typeof(PracticeSummaryDto), "PracticeSummary" }, { typeof(PracticeTrackTotalDto), "PracticeTrackTotal" },
        { typeof(DeletedCountDto), "DeletedCount" }, { typeof(LoopRegionDto), "LoopRegion" },
    };

    [Fact]
    public void Backend_IsLabelled1130AndListsTheMergedDeltas()
    {
        var info = Backend.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.13.0");
        Strings(info["x-stemstudio-contains"]!).ShouldBe(["1.1.0", "1.2.0", "1.3.0", "1.4.0", "1.5.0", "1.6.0", "1.7.0", "1.8.0", "1.9.0", "1.10.0", "1.21.0", "1.11.0", "1.12.0", "1.22.0", "1.23.0", "1.13.0"]);
        Backend014Delta.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.13.0-delta.2");
    }

    [Theory]
    [MemberData(nameof(Changed1120Entries))]
    public void Backend1130_Contains1120UnchangedExceptLoopRegion(string section, string[] changed)
    {
        ChangedEntries(BaseDocument, Backend, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Sections), MemberType = typeof(ContractJson))]
    public void Backend1130_ContainsEveryDeltaEntryUnchanged(string section)
    {
        var shared = SharedComponents.Where(c => section.EndsWith(c.Split('/')[0], StringComparison.Ordinal)).Select(c => c.Split('/')[1]);
        var present = section.Split('/').Aggregate((JsonNode?)Backend014Delta.Root, (node, part) => node?[part]) is not null;

        (present ? ChangedEntries(Backend014Delta, Backend, section).Except(shared) : []).ShouldBeEmpty();
    }

    [Fact]
    public void Backend1130_AddsTheTwentyTwoPracticeFlowOperations()
    {
        var added = Backend.Operations.Select(Id).Except(BaseDocument.Operations.Select(Id)).ToList();

        added.ShouldBe(Backend014Delta.Operations.Select(Id), ignoreOrder: true);
        added.Count.ShouldBe(22);
    }

    [Fact]
    public void PracticeFlowOperations_CarryNoWriteGate()
    {
        // Database edits and job starts only (013 data-model 5.2 "Allowed"); maintenance (503) is the middleware's.
        Backend014Delta.Operations.ShouldAllBe(op => op.Operation["x-stemstudio-write-gate"] == null);
    }

    [Fact]
    public void LoopRegion_GainsOnlyTheOptionalSectionId()
    {
        var after = Schema("LoopRegion").DeepClone().AsObject();
        var sectionId = after["properties"]!.AsObject()["sectionId"]!;

        Strings(sectionId["type"]!).ShouldBe(["string", "null"]);
        Strings(after["required"]!).ShouldNotContain("sectionId");
        after["properties"]!.AsObject().Remove("sectionId");
        JsonNode.DeepEquals(BaseDocument.Root["components"]!["schemas"]!["LoopRegion"], after).ShouldBeTrue();
    }

    [Fact]
    public void LoopRegionDto_OmitsANullSectionIdAndKeepsAGivenOne()
    {
        var id = Guid.Parse("5b1f7c2e-1d3a-4b5c-8d9e-0f1a2b3c4d5e");

        JsonSerializer.Serialize(new LoopRegionDto(1, 2, true), Web).ShouldBe("""{"start":1,"end":2,"enabled":true}""");
        var withId = JsonSerializer.SerializeToNode(new LoopRegionDto(1, 2, true, id), Web)!;
        withId["sectionId"]!.GetValue<Guid>().ShouldBe(id);
        SchemaValidator.Validate(OpenApiContract.Backend, Schema("LoopRegion"), withId).ShouldBeEmpty();
        JsonSerializer.Deserialize<LoopRegionDto>("""{"start":1,"end":2,"enabled":true}""", Web)!.SectionId.ShouldBeNull();
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_RoundTripsAndValidates(string example, Type type)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize(type, Web), type, Web)!;

        JsonNode.DeepEquals(Canonical(json), Canonical(again)).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(OpenApiContract.Backend, Schema(type.Name.Replace("Dto", string.Empty, StringComparison.Ordinal)), again).ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(DtoSchemas))]
    public void Dto_PropertiesAreTheSchemaProperties(Type type, string schema)
    {
        var names = type.GetProperties().Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        names.ShouldBe(Schema(schema)["properties"]!.AsObject().Select(p => p.Key), ignoreOrder: true);
    }

    [Fact]
    public void Enums_MatchTheirSchemas()
    {
        AssertEnumsMatch([(typeof(SectionKindDto), "SectionKind"), (typeof(SectionSetStatusDto), "SectionSetStatus"), (typeof(ScoreViewStatusDto), "ScoreViewStatus")]);
        WireNames(typeof(SectionWarningCodeDto)).ShouldBe(InlineEnum("SectionWarning", "code"));
        WireNames(typeof(SectionOriginDto)).ShouldBe(InlineEnum("Section", "origin"));
        WireNames(typeof(SectionSetOriginDto)).ShouldBe(InlineEnum("SectionSet", "origin"));
        WireNames(typeof(SectionSnapDto)).ShouldBe(InlineEnum("SectionSet", "snap").Where(v => v.Length > 0));
        WireNames(typeof(PracticeSectionKindDto)).ShouldBe(InlineEnum("PracticeSectionInput", "kind"));
    }

    [Fact]
    public void Requests_SerializeToValidBodies()
    {
        var track = Guid.NewGuid();
        var bodies = new (object Dto, string Schema)[]
        {
            (new SectionsUpdateRequest { Revision = 3, Sections = [new SectionInputDto { Kind = SectionKindDto.Solo, Label = "Guitar solo", StartSeconds = 0, EndSeconds = 8, Instrument = "guitar" }] }, "SectionsUpdate"),
            (new DetectSectionsRequest { Overwrite = true }, "DetectSectionsRequest"),
            (new SetlistCreateRequest { Name = "Band night" }, "SetlistCreate"),
            (new SetlistUpdateRequest { Note = "Bring the capo." }, "SetlistUpdate"),
            (new SetlistEntriesUpdateRequest { Entries = [new SetlistEntryInputDto { TrackId = track }] }, "SetlistEntriesUpdate"),
            (new PracticeLogSettingsUpdateRequest { Enabled = false }, "PracticeLogSettingsUpdate"),
            (new PracticeSessionInputDto
            {
                TrackId = track, StartedAt = DateTimeOffset.UnixEpoch, EndedAt = DateTimeOffset.UnixEpoch.AddMinutes(5), PlayedSeconds = 290,
                Sections = [new PracticeSectionInputDto { Kind = PracticeSectionKindDto.None, Label = string.Empty, PlayedSeconds = 290 }],
            }, "PracticeSessionInput"),
        };

        foreach (var (dto, schema) in bodies)
        {
            SchemaValidator.Validate(OpenApiContract.Backend, Schema(schema), JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)).ShouldBeEmpty(schema);
        }
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchContractPaths(string route, string method)
    {
        var path = route.Split('?')[0];

        OpenApiContract.Backend.Operations.ShouldContain(op => op.Method == method && Template(op.Path).IsMatch(path), route);
    }

    public static TheoryData<string, string> Routes()
    {
        var id = Guid.NewGuid();
        return new()
        {
            { ApiRoutes.Sections(id), "get" }, { ApiRoutes.Sections(id), "put" }, { ApiRoutes.SectionsDetect(id), "post" },
            { ApiRoutes.TrackScores(id), "get" }, { ApiRoutes.ScoreView(id), "get" }, { ApiRoutes.ScoreView(id), "post" },
            { ApiRoutes.ScoreViewPage(id, 2), "get" }, { ApiRoutes.Setlists, "get" }, { ApiRoutes.Setlists, "post" },
            { ApiRoutes.Setlist(id), "get" }, { ApiRoutes.SetlistAcknowledgingRemoved(id), "get" }, { ApiRoutes.Setlist(id), "patch" },
            { ApiRoutes.Setlist(id), "delete" }, { ApiRoutes.SetlistEntries(id), "put" }, { ApiRoutes.SetlistPdf(id, SetlistPdfOptions.Default), "get" },
            { ApiRoutes.PracticeLogSettings, "get" }, { ApiRoutes.PracticeLogSettings, "put" }, { ApiRoutes.PracticeSessions, "get" },
            { ApiRoutes.PracticeSessionsOf(id), "delete" }, { ApiRoutes.PracticeSession(id), "put" }, { ApiRoutes.PracticeSession(id), "delete" },
            { ApiRoutes.PracticeSummaryFor(id, 30, -60), "get" }, { ApiRoutes.PracticeOverviewFor(120), "get" },
        };
    }

    [Fact]
    public void RouteQueries_UseTheContractParameterNames()
    {
        var id = Guid.Parse("3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b");

        ApiRoutes.SetlistPdf(id, new SetlistPdfOptions(Lyrics: false, PageBreakPerSong: false))
            .ShouldBe($"/api/setlists/{id}/pdf?lyrics=false&sections=true&notes=true&pageBreakPerSong=false");
        ApiRoutes.PracticeSummaryFor(id, 7, -90).ShouldBe($"/api/practice/summary?trackId={id}&days=7&utcOffsetMinutes=-90");
        ApiRoutes.PracticeOverviewFor(60).ShouldBe("/api/practice/overview?utcOffsetMinutes=60");
        ApiRoutes.ScoreViewPage(id, 12).ShouldBe($"/api/assets/{id}/score-view/pages/12");
        var pdf = Operation(OpenApiContract.Backend, "/api/setlists/{setlistId}/pdf", "get")["parameters"]!.AsArray().Select(p => p!["name"]!.GetValue<string>());
        pdf.ShouldBe(["lyrics", "sections", "notes", "pageBreakPerSong"], ignoreOrder: true);
    }

    [Fact]
    public void Limits_MatchTheSchemas()
    {
        Int(Prop("SectionsUpdate", "sections"), "maxItems").ShouldBe(ContractLimits.MaxSections);
        Int(Prop("SectionInput", "label"), "maxLength").ShouldBe(ContractLimits.MaxSectionLabel);
        Int(Prop("SetlistEntriesUpdate", "entries"), "maxItems").ShouldBe(ContractLimits.MaxSetlistEntries);
        Int(Prop("SetlistCreate", "name"), "maxLength").ShouldBe(ContractLimits.MaxSetlistName);
        Int(Prop("SetlistCreate", "note"), "maxLength").ShouldBe(ContractLimits.MaxSetlistNote);
        Int(Prop("SetlistEntryInput", "note"), "maxLength").ShouldBe(ContractLimits.MaxEntryNote);
        Int(Prop("PracticeSessionInput", "playedSeconds"), "maximum").ShouldBe((int)ContractLimits.MaxSessionPlayedSeconds);
        Int(Prop("ScoreView", "pages"), "maxItems").ShouldBe(ContractLimits.MaxScoreViewPages);
        Int(Prop("ScoreView", "bars"), "maxItems").ShouldBe(ContractLimits.MaxScoreViewBars);
        var offset = OpenApiContract.Backend.Root["components"]!["parameters"]!["UtcOffsetMinutes"]!["schema"]!;
        (Int(offset, "minimum"), Int(offset, "maximum")).ShouldBe((ContractLimits.MinUtcOffsetMinutes, ContractLimits.MaxUtcOffsetMinutes));
    }

    [Fact]
    public void ProblemTypes_AreNamedInTheContract()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "014", "practice-api.openapi.yaml"));
        string[] codes =
        [
            PracticeFlowProblemTypes.SectionsChanged, PracticeFlowProblemTypes.SectionsEdited, PracticeFlowProblemTypes.DetectionRunning,
            PracticeFlowProblemTypes.TrackNotReady, PracticeFlowProblemTypes.NotAScore, PracticeFlowProblemTypes.ScoreNotReady,
            PracticeFlowProblemTypes.ScoreViewNotReady, PracticeFlowProblemTypes.SetlistEmpty, PracticeFlowProblemTypes.PracticeLogOff,
            PracticeFlowProblemTypes.DocumentTimeout, PracticeFlowProblems.DocumentFailed, PracticeFlowProblemTypes.TrackNotFound, PracticeFlowProblemTypes.MixTrackMismatch,
        ];

        codes.ShouldAllBe(code => text.Contains(code, StringComparison.Ordinal));
        PracticeFlowProblemTypes.BaseUri.ShouldBe(Contracts.LibraryCare.LibraryCareProblemTypes.BaseUri);
    }

    [Fact]
    public void SetlistPdf503_NamesWorkerUnavailableAndDocumentFailed_InTheDelta_1130_And1140()
    {
        foreach (var contract in new[] { Backend014Delta, Backend, OpenApiContract.Backend })
        {
            var response = contract.Response("getSetlistPdf", 503)!;
            var example = response["content"]!["application/problem+json"]!["example"]!;

            response["description"]!.GetValue<string>().ShouldContain("`worker-unavailable`");
            response["description"]!.GetValue<string>().ShouldContain($"`{PracticeFlowProblems.DocumentFailed}`");
            example["type"]!.GetValue<string>().ShouldBe(PracticeFlowProblemTypes.BaseUri + PracticeFlowProblems.DocumentFailed);
            example["status"]!.GetValue<double>().ShouldBe(503);
        }

        OpenApiContract.Backend.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.14.0");
    }

    [Fact]
    public void Worker180_Contains170ExceptTheAdditiveHealthFields()
    {
        WorkerContract.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.8.0");
        ChangedEntries(Worker013, WorkerContract, "components/schemas").ShouldBe(["Health", "LanesHealth"], ignoreOrder: true);
        ChangedEntries(Worker013, WorkerContract, "paths").ShouldBe(["/health", "/v1/render-jobs"], ignoreOrder: true);
        ChangedEntries(Worker013, WorkerContract, "components/examples").ShouldBeEmpty();
        ChangedEntries(Worker013, WorkerContract, "components/responses").ShouldBeEmpty();
        Strings(WorkerContract.Root["components"]!["schemas"]!["LanesHealth"]!["required"]!).ShouldContain("practice");
    }

    [Theory]
    [InlineData("section-job-request", "SectionJobRequestSixStems")]
    [InlineData("section-queued-job", "SectionJobQueued")]
    [InlineData("section-completed-job", "SectionJobCompleted")]
    [InlineData("section-failed-job", "SectionJobFailed")]
    [InlineData("score-view-request", "ScoreViewJobRequestBass")]
    [InlineData("score-view-completed-job", "ScoreViewJobCompleted")]
    [InlineData("setlist-document-request", "SetlistDocumentRequestTwoSongs")]
    [InlineData("setlist-document-completed-job", "SetlistDocumentJobCompleted")]
    public void WorkerPracticeExamples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        IsCopyOf(file, WorkerExample(example)).ShouldBeTrue(file);
    }

    private static JsonNode Prop(string schema, string property) => Schema(schema)["properties"]![property]!;

    private static IEnumerable<string> InlineEnum(string schema, string property) =>
        Prop(schema, property)["enum"]!.AsArray().Select(v => v?.GetValue<string>() ?? string.Empty);

    private static int Int(JsonNode node, string member) => int.Parse(node[member]!.ToString(), CultureInfo.InvariantCulture);

    private static string Id((string Path, string Method, JsonObject Operation) op) => op.Operation["operationId"]!.GetValue<string>();

    private static JsonObject Operation(OpenApiContract contract, string path, string method) => contract.Root["paths"]![path]![method]!.AsObject();

    private static Regex Template(string path) =>
        new("^" + Regex.Replace(Regex.Escape(path), @"\\\{[A-Za-z]+\}", "[^/]+") + "$", RegexOptions.None, TimeSpan.FromSeconds(1));

    // Date-time strings in canonical UTC form, so "Z" and "+00:00" compare equal (same instant).
    private static JsonNode? Canonical(JsonNode? node) => node switch
    {
        JsonObject obj => new JsonObject(obj.Select(p => KeyValuePair.Create(p.Key, Canonical(p.Value)))),
        JsonArray array => new JsonArray([.. array.Select(Canonical)]),
        JsonValue value when value.TryGetValue<string>(out var text) && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time)
            && text.Contains('T', StringComparison.Ordinal) => JsonValue.Create(time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)),
        _ => node?.DeepClone(),
    };
}
