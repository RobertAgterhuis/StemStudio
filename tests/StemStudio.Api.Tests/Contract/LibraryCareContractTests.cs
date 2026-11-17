using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StemStudio.Contracts;
using StemStudio.Contracts.LibraryCare;
using StemStudio.Contracts.Tracks;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 013 Phase 0, T005/T006): backend-api 1.12.0 (<c>specs/013-library-care/contracts/backend-api.openapi.yaml</c>,
/// linked as <c>Contract/013</c> since 015 Phase 0; the other tests read the current 1.14.0, a superset)
/// is 012's 1.11.0 plus exactly the 013 delta (DELTA.md section 1 and "Phase 0"), and the LibraryCare DTOs, routes and limits match it.
/// </summary>
public sealed partial class LibraryCareContractTests
{
    private static readonly string[] GatedPaths =
    [
        "/api/tracks/{trackId}", "/api/tracks/{trackId}/retry", "/api/tracks/{trackId}/artwork", "/api/assets/{assetId}",
        "/api/generations/{generationId}", "/api/voice-profiles/{profileId}", "/api/styles/{styleId}", "/api/lyrics/{lyricsId}",
        "/api/agent/conversations/{conversationId}",
    ];

    private static readonly (string Path, string Method)[] GatedOperations =
    [
        ("/api/tracks/{trackId}", "delete"), ("/api/tracks/{trackId}/retry", "post"), ("/api/tracks/{trackId}/artwork", "put"),
        ("/api/tracks/{trackId}/artwork", "delete"), ("/api/assets/{assetId}", "delete"), ("/api/generations/{generationId}", "delete"),
        ("/api/voice-profiles/{profileId}", "delete"), ("/api/styles/{styleId}", "delete"), ("/api/lyrics/{lyricsId}", "delete"),
        ("/api/agent/conversations/{conversationId}", "delete"),
    ];

    // Delta components that already exist in 1.11.0 (kept there) or only carry a merge note (DELTA.md "Phase 0").
    private static readonly HashSet<string> SharedComponents =
        ["responses/NotFound", "responses/ValidationProblem", "schemas/ProblemDetails", "schemas/HttpValidationProblemDetails", "schemas/DetailPhaseAddition"];

    public static TheoryData<string, string[]> Changed1110Entries => new()
    {
        { "paths", GatedPaths },
        { "components/schemas", ["DetailPhase"] },
        { "components/responses", [] },
        { "components/examples", [] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string, Type> Examples => new()
    {
        { "StatusIdle", typeof(LibraryCareStatusDto) },
        { "StatusBackupRunning", typeof(LibraryCareStatusDto) },
        { "BackupListTwo", typeof(BackupListDto) },
        { "EstimateDefaults", typeof(BackupEstimateDto) },
        { "BackupJobQueued", typeof(LibraryJobDto) },
        { "BackupJobRunning", typeof(LibraryJobDto) },
        { "BackupJobCompleted", typeof(LibraryJobDto) },
        { "RestoreJobExtracting", typeof(LibraryJobDto) },
        { "RestoreJobCompleted", typeof(LibraryJobDto) },
        { "RestoreJobFailedUnsafe", typeof(LibraryJobDto) },
        { "StorageSmall", typeof(StorageOverviewDto) },
        { "CleanupPlanDetail", typeof(CleanupPlanDto) },
    };

    [Fact]
    public void Backend_IsLabelled1120AndListsTheMergedDeltas()
    {
        var info = Backend013.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.12.0");
        Strings(info["x-stemstudio-contains"]!).ShouldBe(["1.1.0", "1.2.0", "1.3.0", "1.4.0", "1.5.0", "1.6.0", "1.7.0", "1.8.0", "1.9.0", "1.10.0", "1.21.0", "1.11.0", "1.12.0"]);
    }

    [Theory]
    [MemberData(nameof(Changed1110Entries))]
    public void Backend1120_Contains1110UnchangedExceptTheGateAndDetailPhase(string section, string[] changed)
    {
        ChangedEntries(Backend012, Backend013, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(Sections), MemberType = typeof(ContractJson))]
    public void Backend1120_ContainsEveryDeltaEntryUnchanged(string section)
    {
        var shared = SharedComponents.Where(c => section.EndsWith(c.Split('/')[0], StringComparison.Ordinal)).Select(c => c.Split('/')[1]);
        var present = section.Split('/').Aggregate((JsonNode?)Backend013Delta.Root, (node, part) => node?[part]) is not null;

        (present ? ChangedEntries(Backend013Delta, Backend013, section).Except(shared) : []).ShouldBeEmpty();
    }

    [Fact]
    public void Backend1120_AddsTheEighteenLibraryCareOperations()
    {
        var added = Backend013.Operations.Select(Id).Except(Backend012.Operations.Select(Id)).ToList();

        added.ShouldBe(Backend013Delta.Operations.Select(Id), ignoreOrder: true);
        added.Count.ShouldBe(18);
    }

    [Fact]
    public void GatedOperations_OnlyGainTheGateResponses()
    {
        foreach (var (path, method) in GatedOperations)
        {
            var before = Operation(Backend012, path, method);
            var after = Operation(Backend013, path, method).DeepClone().AsObject();

            after["x-stemstudio-write-gate"]!.GetValue<string>().ShouldBe("backupRunning", path);
            Codes(after).ShouldContain("409", path);
            Codes(after).ShouldContain("503", path);
            RemoveGateAdditions(after, before);
            JsonNode.DeepEquals(before, after).ShouldBeTrue($"{method} {path}");
        }
    }

    [Fact]
    public void DetailPhase_GainsOnlyTheOptionalFilesRemovedAt()
    {
        var after = Schema("DetailPhase").DeepClone().AsObject();

        Strings(after["properties"]!["filesRemovedAt"]!["type"]!).ShouldBe(["string", "null"]);
        Strings(after["required"]!).ShouldNotContain("filesRemovedAt");
        after["properties"]!.AsObject().Remove("filesRemovedAt");
        JsonNode.DeepEquals(Backend012.Root["components"]!["schemas"]!["DetailPhase"], after).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public void Example_RoundTripsAndValidates(string example, Type type)
    {
        var json = Example(example);
        var again = JsonSerializer.SerializeToNode(json.Deserialize(type, Web), type, Web)!;

        JsonNode.DeepEquals(Canonical(json), Canonical(again)).ShouldBeTrue(again.ToJsonString());
        SchemaValidator.Validate(Backend013, Schema(SchemaOf(type)), again).ShouldBeEmpty();
    }

    [Fact]
    public void Reports_AreToldApartByTheirType()
    {
        var restore = Example("RestoreJobCompleted").Deserialize<LibraryJobDto>(Web)!;
        var backup = Example("BackupJobCompleted").Deserialize<LibraryJobDto>(Web)!;

        restore.Report.ShouldBeOfType<RestoreReportDto>().PreviousLibraryKept.ShouldBeTrue();
        backup.Report.ShouldBeOfType<BackupReportDto>().Warnings[0].Code.ShouldBe(ArchiveWarningCodeDto.ChangedAfterSnapshot);
        JsonSerializer.SerializeToNode<LibraryJobReportDto>(Samples.Verify, Web)!["type"]!.GetValue<string>().ShouldBe("verify");
    }

    [Theory]
    [MemberData(nameof(SampleInstances))]
    public void ConstructedDtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        // Reports are only written through LibraryJob.report (the base type), which adds the `type` discriminator.
        var node = JsonSerializer.SerializeToNode(dto, dto is LibraryJobReportDto ? typeof(LibraryJobReportDto) : dto.GetType(), Web);

        SchemaValidator.Validate(Backend013, Schema(schema), node).ShouldBeEmpty();
    }

    public static TheoryData<string, object> SampleInstances => new()
    {
        { "BackupDetails", Samples.Details },
        { "VerifyReport", Samples.Verify },
        { "CleanupResult", Samples.CleanupResult },
        { "PreviousLibrary", Samples.Previous },
        { "RestoreOptions", new RestoreOptionsDto { Mode = RestoreModeDto.Replace, Confirmation = LibraryCareLimits.ReplaceConfirmation } },
        { "Confirmation", new ConfirmationDto { Confirmation = LibraryCareLimits.DiscardConfirmation } },
        { "CleanupRequest", new CleanupRequestDto { Actions = [new CleanupActionDto { Kind = CleanupKindDto.DetailStems, TrackIds = [Guid.NewGuid()] }] } },
        { "ExecuteCleanupRequest", new ExecuteCleanupRequestDto { PlanId = Guid.NewGuid() } },
        { "BackupOptions", new BackupOptionsDto { DetailStems = false } },
    };

    [Fact]
    public void ComponentEnums_MatchTheirSchemas()
    {
        AssertEnumsMatch([
            (typeof(BackupCategoryDto), "Category"), (typeof(StorageCategoryDto), "StorageCategory"), (typeof(RegenerabilityDto), "Regenerability"),
            (typeof(LibraryJobKindDto), "LibraryJobKind"), (typeof(LibraryJobStatusDto), "LibraryJobStatus"), (typeof(GateModeDto), "GateMode"),
            (typeof(CleanupKindDto), "CleanupKind"),
        ]);
    }

    [Fact]
    public void InlineEnums_MatchTheirSchemaProperties()
    {
        WireNames(typeof(ActiveWorkKindDto)).ShouldBe(InlineEnum("ActiveWork", "kind"));
        WireNames(typeof(ArchiveWarningCodeDto)).ShouldBe(InlineEnum("ArchiveWarning", "code"));
        WireNames(typeof(RestoreModeDto)).ShouldBe(InlineEnum("RestoreOptions", "mode"));
        WireNames(typeof(RestoreReportModeDto)).ShouldBe(InlineEnum("RestoreReport", "mode"));
        WireNames(typeof(LibraryJobErrorCodeDto)).ShouldBe(InlineEnum("JobError", "code"));
        WireNames(typeof(SkippedItemReasonDto)).ShouldBe(InlineEnum("SkippedItem", "reason"));
        WireNames(typeof(StorageVolumeIdDto)).ShouldBe(InlineEnum("Volume", "id"));
        WireNames(typeof(CleanupSkipReasonDto)).ShouldBe(InlineEnum("CleanupSkip", "reason"));
        WireNames(typeof(LibraryJobStageDto)).Append(null).ShouldBe(Schema("LibraryJob")["properties"]!["stage"]!["enum"]!.AsArray().Select(v => v?.GetValue<string>()));
    }

    [Fact]
    public void RequestDefaults_MatchTheSchemaDefaults()
    {
        var options = JsonSerializer.Deserialize<BackupOptionsDto>("{}", Web)!;
        var restore = JsonSerializer.Deserialize<RestoreOptionsDto>("""{"mode":"merge"}""", Web)!;

        options.ShouldBe(new BackupOptionsDto());
        new[] { options.DetailStems, options.Exports, options.Scores, options.Generations, options.Recordings, options.VoiceProfiles }.ShouldAllBe(v => v);
        Schema("BackupOptions")["properties"]!.AsObject().ShouldAllBe(p => p.Value!["default"]!.GetValue<bool>());
        (restore.VoiceProfiles, restore.Confirmation).ShouldBe((true, (string?)null));
    }

    [Fact]
    public void Limits_MatchTheContract()
    {
        var root = Backend013.Root["components"]!;
        var limit = Operation(Backend013, "/api/library-jobs", "get")["parameters"]![0]!["schema"]!;

        root["parameters"]!["FileName"]!["schema"]!["pattern"]!.GetValue<string>().ShouldBe(LibraryCareLimits.ArchiveFileNamePattern);
        Schema("BackupSummary")["properties"]!["fileName"]!["pattern"]!.GetValue<string>().ShouldBe(LibraryCareLimits.ArchiveFileNamePattern);
        (Int(limit, "minimum"), Int(limit, "maximum"), Int(limit, "default"))
            .ShouldBe((LibraryCareLimits.MinJobListLimit, LibraryCareLimits.MaxJobListLimit, LibraryCareLimits.DefaultJobListLimit));
        Int(Schema("Confirmation")["properties"]!["confirmation"]!, "maxLength").ShouldBe(LibraryCareLimits.MaxConfirmationLength);
        Int(Schema("CleanupRequest")["properties"]!["actions"]!, "maxItems").ShouldBe(LibraryCareLimits.MaxCleanupActions);
        Int(Schema("CleanupAction")["properties"]!["trackIds"]!, "maxItems").ShouldBe(LibraryCareLimits.MaxCleanupTrackIds);
        Int(root["responses"]!["Maintenance"]!["headers"]!["Retry-After"]!, "example").ShouldBe(LibraryCareLimits.MaintenanceRetryAfterSeconds);
    }

    [Fact]
    public void ArchiveFileNamePattern_AcceptsWrittenNamesAndRefusesPaths()
    {
        var written = LibraryCareLimits.ArchiveFileNamePrefix
            + new DateTime(2026, 11, 16, 14, 22, 33, DateTimeKind.Utc).ToString(LibraryCareLimits.ArchiveFileNameTimeFormat, CultureInfo.InvariantCulture) + ".zip";

        written.ShouldBe("stemstudio-backup-20261116-142233.zip");
        FileName().IsMatch(written).ShouldBeTrue();
        new[] { "../a.zip", "a/b.zip", ".hidden.zip", "a.zip.partial", "a.ZIP", string.Concat(Enumerable.Repeat("a", 121)) + ".zip" }
            .ShouldAllBe(name => !FileName().IsMatch(name));
    }

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchContractPaths(string route, string method)
    {
        var path = route.Split('?')[0];

        Backend013.Operations.ShouldContain(op => op.Method == method && Template(op.Path).IsMatch(path), route);
    }

    public static TheoryData<string, string> Routes()
    {
        var job = Guid.NewGuid();
        const string name = "stemstudio-backup-20261116-142233.zip";
        return new()
        {
            { ApiRoutes.LibraryCareStatus, "get" }, { ApiRoutes.Backups, "get" }, { ApiRoutes.Backups, "post" },
            { ApiRoutes.BackupEstimateFor(new BackupOptionsDto { Scores = false }), "get" }, { ApiRoutes.Backup(name), "get" },
            { ApiRoutes.Backup(name), "delete" }, { ApiRoutes.BackupFile(name), "get" }, { ApiRoutes.VerifyBackup(name), "post" },
            { ApiRoutes.RestoreBackup(name), "post" }, { ApiRoutes.LibraryJobsWithLimit(5), "get" }, { ApiRoutes.LibraryJob(job), "get" },
            { ApiRoutes.CancelLibraryJob(job), "post" }, { ApiRoutes.PreviousLibrary, "get" }, { ApiRoutes.RevertRestore, "post" },
            { ApiRoutes.DiscardPreviousLibrary, "post" }, { ApiRoutes.StorageOverview(refresh: true), "get" },
            { ApiRoutes.StorageOverview(refresh: false), "get" }, { ApiRoutes.CleanupPreview, "post" }, { ApiRoutes.Cleanup, "post" },
        };
    }

    [Fact]
    public void RouteQueries_CarryEveryOption()
    {
        ApiRoutes.BackupEstimateFor(new BackupOptionsDto { Scores = false, VoiceProfiles = false })
            .ShouldBe("/api/backups/estimate?detailStems=true&exports=true&scores=false&generations=true&recordings=true&voiceProfiles=false");
        ApiRoutes.LibraryJobsWithLimit(50).ShouldBe("/api/library-jobs?limit=50");
        ApiRoutes.StorageOverview(refresh: true).ShouldBe("/api/storage?refresh=true");
        Should.Throw<ArgumentNullException>(() => ApiRoutes.BackupEstimateFor(null!));
    }

    [Fact]
    public void DetailPhaseDto_OmitsFilesRemovedAtWhenNullAndRoundTripsIt()
    {
        var removed = DetailPhaseDto.None with { Status = DetailPhaseStatusDto.Completed, FilesRemovedAt = new DateTimeOffset(2026, 11, 16, 14, 0, 0, TimeSpan.Zero) };
        var node = JsonSerializer.SerializeToNode(removed, Web)!;

        JsonSerializer.SerializeToNode(DetailPhaseDto.None, Web)!.AsObject().ContainsKey("filesRemovedAt").ShouldBeFalse();
        node.Deserialize<DetailPhaseDto>(Web)!.FilesRemovedAt.ShouldBe(removed.FilesRemovedAt);
        SchemaValidator.Validate(Backend013, Schema("DetailPhase"), node).ShouldBeEmpty();
    }

    [Fact]
    public void ProblemTypes_AreTheContractCodes()
    {
        var text = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "013", "backend-api.delta.openapi.yaml"));
        var codes = typeof(LibraryCareProblemTypes).GetFields().Where(f => f.Name != nameof(LibraryCareProblemTypes.BaseUri)).Select(f => (string)f.GetValue(null)!).ToList();

        codes.Count.ShouldBe(17);
        codes.ShouldAllBe(code => text.Contains(code, StringComparison.Ordinal));
        LibraryCareProblemTypes.BaseUri.ShouldBe(AgentProblemBase());
    }

    private static string AgentProblemBase() => Contracts.Agent.AgentProblemTypes.BaseUri;

    private static string Id((string Path, string Method, JsonObject Operation) op) => op.Operation["operationId"]!.GetValue<string>();

    private static JsonObject Operation(OpenApiContract contract, string path, string method) => contract.Root["paths"]![path]![method]!.AsObject();

    private static List<string> Codes(JsonObject operation) => [.. operation["responses"]!.AsObject().Select(r => r.Key)];

    private static void RemoveGateAdditions(JsonObject after, JsonObject before)
    {
        after.Remove("x-stemstudio-write-gate");
        var responses = after["responses"]!.AsObject();
        foreach (var code in new[] { "409", "503" }.Where(code => before["responses"]![code] is null))
        {
            responses.Remove(code);
        }
    }

    private static IEnumerable<string> InlineEnum(string schema, string property) => Strings(Schema(schema)["properties"]![property]!["enum"]!);

    private static int Int(JsonNode node, string member) => int.Parse(node[member]!.ToString(), CultureInfo.InvariantCulture);

    private static string SchemaOf(Type type) => type.Name.Replace("Dto", string.Empty, StringComparison.Ordinal);

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

    [GeneratedRegex(LibraryCareLimits.ArchiveFileNamePattern)]
    private static partial Regex FileName();
}
