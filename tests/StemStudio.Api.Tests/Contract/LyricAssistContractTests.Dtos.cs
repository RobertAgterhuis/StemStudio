using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>023 Phase 0 (T005): limits, routes and DTO serialisation against backend-api 1.23.0.</summary>
public sealed partial class LyricAssistContractTests
{
    private static readonly Guid SheetId = Guid.Parse("2c4e6a8b-0d1f-4a3b-8c5d-7e9f1a3b5c7d");

    private static readonly Guid AssistId = Guid.Parse("5a6b7c8d-9e0f-4a1b-8c2d-3e4f5a6b7c8d");

    private static readonly Guid ItemId = Guid.Parse("9f8e7d6c-5b4a-4c3d-9e2f-1a0b9c8d7e6f");

    private static readonly DateTimeOffset At = new(2026, 11, 17, 9, 30, 0, TimeSpan.Zero);

    private static readonly Lazy<JsonObject> Samples = new(() =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Contract", "LyricAssistSamples.json")))!.AsObject());

    [Theory]
    [InlineData("CreateLyricAssistRequest", "count", LyricAssistLimits.MinCount, LyricAssistLimits.MaxAlternatives)]
    [InlineData("RhythmTarget", "bpm", LyricAssistLimits.MinBpm, LyricAssistLimits.MaxBpm)]
    [InlineData("RhythmTarget", "beatsPerBar", LyricAssistLimits.MinBeatsPerBar, LyricAssistLimits.MaxBeatsPerBar)]
    [InlineData("RhythmTarget", "barsPerLine", LyricAssistLimits.MinBarsPerLine, LyricAssistLimits.MaxBarsPerLine)]
    [InlineData("RhythmTarget", "syllablesPerBeat", LyricAssistLimits.MinSyllablesPerBeat, LyricAssistLimits.MaxSyllablesPerBeat)]
    [InlineData("LanguageGuess", "confidence", 0, 1)]
    public void Ranges_MatchTheLimits(string schema, string property, double min, double max)
    {
        var node = Schema(schema)["properties"]![property]!;

        (node["minimum"]!.GetValue<double>(), node["maximum"]!.GetValue<double>()).ShouldBe((min, max));
    }

    [Theory]
    [InlineData("CreateLyricAssistRequest", "instruction", LyricAssistLimits.InstructionMaxLength)]
    [InlineData("LyricSuggestion", "explanation", LyricAssistLimits.ExplanationMaxLength)]
    [InlineData("LyricSuggestion", "proposal", LyricAssistLimits.ProposalMaxLength)]
    [InlineData("Anchor", "original", LyricAssistLimits.ProposalMaxLength)]
    [InlineData("IntentionalMarkInput", "text", LyricAssistLimits.MarkLineMaxLength)]
    [InlineData("IntentionalMarkInput", "note", LyricAssistLimits.NoteMaxLength)]
    [InlineData("LyricWordInput", "word", LyricAssistLimits.WordMaxLength)]
    [InlineData("OwnWorkConfirmRequest", "confirmedBy", LyricAssistLimits.ConfirmedByMaxLength)]
    [InlineData("OwnWorkConfirmRequest", "statementVersion", LyricAssistLimits.StatementVersionMaxLength)]
    [InlineData("OwnWorkOverride", "matchedTrackTitle", LyricAssistLimits.MatchedTrackTitleMaxLength)]
    [InlineData("SyllableRequest", "text", LyricAssistLimits.RawTextMaxLength)]
    [InlineData("LanguageGuessRequest", "text", LyricAssistLimits.RawTextMaxLength)]
    public void Lengths_MatchTheLimits(string schema, string property, int max)
    {
        ((int)Schema(schema)["properties"]![property]!["maxLength"]!.GetValue<double>()).ShouldBe(max);
    }

    [Theory]
    [InlineData("LyricAssist", LyricAssistLimits.MaxSuggestions)]
    [InlineData("SuggestionList", LyricAssistLimits.MaxSuggestions)]
    [InlineData("SuggestionDecisionRequest", LyricAssistLimits.MaxDecisionIds)]
    [InlineData("CreateLyricAssistRequest", LyricAssistLimits.MaxCategories)]
    [InlineData("RhymeResult", LyricAssistLimits.MaxRhymes)]
    public void ArrayLimits_MatchTheLimits(string schema, int max)
    {
        var properties = Schema(schema)["allOf"] is JsonArray parts ? parts[1]!["properties"]! : Schema(schema)["properties"]!;
        var array = properties.AsObject().Single(p => (p.Value!["type"] as JsonValue)?.GetValue<string>() == "array").Value!;

        ((int)array["maxItems"]!.GetValue<double>()).ShouldBe(max);
    }

    [Theory]
    [InlineData("listLyricAssists", LyricAssistLimits.MaxAssistsPerSheet)]
    [InlineData("listIntentionalMarks", LyricAssistLimits.MaxIntentionalMarks)]
    [InlineData("listLyricsRevisions", LyricAssistLimits.RevisionRetention)]
    [InlineData("listLyricWords", LyricAssistLimits.MaxWordsPerLanguage)]
    public void ListLimits_MatchTheLimits(string operationId, int max)
    {
        var schema = Backend.Response(operationId, 200)!["content"]!["application/json"]!["schema"]!;

        ((int)schema["maxItems"]!.GetValue<double>()).ShouldBe(max);
    }

    [Fact]
    public void RhymeLimitParameter_MatchesTheLimits()
    {
        var limit = Backend.Operation("findRhymes")["parameters"]!.AsArray().Single(p => p!["name"]!.GetValue<string>() == "limit")!["schema"]!;

        ((int)limit["default"]!.GetValue<double>(), (int)limit["maximum"]!.GetValue<double>()).ShouldBe((LyricAssistLimits.DefaultRhymes, LyricAssistLimits.MaxRhymes));
    }

    private static readonly (string Route, string Method, string OperationId)[] RouteTable =
    [
        (ApiRoutes.LyricAssistStatus, "get", "getLyricAssistStatus"),
        (ApiRoutes.LyricAssistSettings, "get", "getLyricAssistSettings"),
        (ApiRoutes.LyricAssistSettings, "put", "updateLyricAssistSettings"),
        (ApiRoutes.LyricsAssists(SheetId), "post", "createLyricAssist"),
        (ApiRoutes.LyricsAssists(SheetId), "get", "listLyricAssists"),
        (ApiRoutes.LyricAssistById(AssistId), "get", "getLyricAssist"),
        (ApiRoutes.LyricAssistEvents(AssistId), "get", "streamLyricAssistEvents"),
        (ApiRoutes.LyricAssistCancel(AssistId), "post", "cancelLyricAssist"),
        (ApiRoutes.LyricsSuggestions(SheetId), "get", "listLyricSuggestions"),
        (ApiRoutes.LyricsSuggestionsAccept(SheetId), "post", "acceptLyricSuggestions"),
        (ApiRoutes.LyricsSuggestionsReject(SheetId), "post", "rejectLyricSuggestions"),
        (ApiRoutes.LyricsSuggestionIntentional(SheetId, ItemId), "post", "markSuggestionIntentional"),
        (ApiRoutes.LyricsIntentionalMarks(SheetId), "get", "listIntentionalMarks"),
        (ApiRoutes.LyricsIntentionalMarks(SheetId), "post", "createIntentionalMark"),
        (ApiRoutes.LyricsIntentionalMark(SheetId, ItemId), "delete", "deleteIntentionalMark"),
        (ApiRoutes.LyricsAssistSettings(SheetId), "put", "updateSheetAssistSettings"),
        (ApiRoutes.LyricsOwnWorkOverride(SheetId), "put", "confirmOwnWork"),
        (ApiRoutes.LyricsOwnWorkOverride(SheetId), "delete", "revokeOwnWork"),
        (ApiRoutes.LyricsUndo(SheetId), "post", "undoLyricSuggestions"),
        (ApiRoutes.LyricsRevisions(SheetId), "get", "listLyricsRevisions"),
        (ApiRoutes.LyricsRevisionRestore(SheetId, 3), "post", "restoreLyricsRevision"),
        (ApiRoutes.LyricsSyllables(SheetId), "get", "getSheetSyllables"),
        (ApiRoutes.LyricAssistSyllables, "post", "countSyllables"),
        (ApiRoutes.LyricAssistRhymes, "get", "findRhymes"),
        (ApiRoutes.LyricAssistWords, "get", "listLyricWords"),
        (ApiRoutes.LyricAssistWords, "post", "addLyricWord"),
        (ApiRoutes.LyricAssistWord(ItemId), "delete", "deleteLyricWord"),
        (ApiRoutes.LyricAssistLanguageGuess, "post", "guessLyricsLanguage"),
    ];

    public static TheoryData<string, string, string> Routes => new(RouteTable);

    [Theory]
    [MemberData(nameof(Routes))]
    public void Routes_MatchTheContractPaths(string route, string method, string operationId)
    {
        var match = Delta.Operations.Where(op => op.Method == method && Matches(op.Path, route)).Select(op => OperationId(op.Operation));

        match.ShouldBe([operationId]);
    }

    [Fact]
    public void Routes_CoverEveryDeltaOperation()
    {
        RouteTable.Select(row => row.OperationId).ShouldBe(Delta.Operations.Select(op => OperationId(op.Operation)), ignoreOrder: true);
    }

    [Fact]
    public void QueryRoutes_UseTheWireNames()
    {
        ApiRoutes.LyricsSuggestions(SheetId, SuggestionStatusDto.Open, SuggestionCategoryDto.Spelling).ShouldBe($"/api/lyrics/{SheetId}/suggestions?status=open&category=spelling");
        ApiRoutes.LyricsSuggestions(SheetId, category: SuggestionCategoryDto.Licence).ShouldBe($"/api/lyrics/{SheetId}/suggestions?category=licence");
        ApiRoutes.LyricAssistRhymesFor("maan licht", AssistLanguageDto.Nl).ShouldBe("/api/lyric-assist/rhymes?word=maan%20licht&language=nl");
        ApiRoutes.LyricAssistRhymesFor("night", AssistLanguageDto.En, 12).ShouldBe("/api/lyric-assist/rhymes?word=night&language=en&limit=12");
        ApiRoutes.LyricAssistWordsFor(AssistLanguageDto.En).ShouldBe("/api/lyric-assist/words?language=en");
        ApiRoutes.LyricAssistEvents(AssistId, 7).ShouldBe($"/api/lyric-assists/{AssistId}/events?after=7");
    }

    [Theory]
    [MemberData(nameof(Dtos))]
    public void Dtos_ValidateAgainstTheirSchemas(string schema, object dto)
    {
        SchemaValidator.Validate(Backend, Schema(schema), JsonSerializer.SerializeToNode(dto, dto.GetType(), Web)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("suggestion", typeof(LyricSuggestionDto))]
    [InlineData("summary", typeof(LyricAssistSummaryDto))]
    [InlineData("assist", typeof(LyricAssistDto))]
    public void Samples_RoundTripUnchanged(string name, Type type)
    {
        var sample = Samples.Value[name]!;
        var again = JsonSerializer.SerializeToNode(sample.Deserialize(type, Web), type, Web)!;

        JsonNode.DeepEquals(WithoutAnyTimes(sample), WithoutAnyTimes(again)).ShouldBeTrue();
    }

    [Fact]
    public void LyricsSheetExample_RoundTripsWithTheAssistDefaults()
    {
        var (expected, actual) = RoundTrip<LyricsSheetDto>("LyricsSheetExample", ignoreTimes: true);
        var dto = Example("LyricsSheetExample").Deserialize<LyricsSheetDto>(Web)!;

        JsonNode.DeepEquals(expected, actual).ShouldBeTrue();
        dto.Revision.ShouldBe(1);
        (dto.Assist with { AssistedLines = [] }).ShouldBe(SheetAssistInfoDto.Default with { AssistedLines = [] });
        dto.Assist.AssistedLines.ShouldBeEmpty();
    }

    [Fact]
    public void BaseDtos_WriteTheNewMembers()
    {
        var input = JsonSerializer.SerializeToNode(new LyricsInput { Title = "T", Language = "nl", Text = "x" }, Web)!.AsObject();
        var page = JsonSerializer.SerializeToNode(new PageContextDto { Page = "lyrics", LyricsId = SheetId }, Web)!;
        var snapshot = JsonSerializer.SerializeToNode(new LyricsSnapshotDto { Title = "T", Language = "nl", Text = "x", AssistedLineCount = 2 }, Web)!;

        var plain = JsonSerializer.SerializeToNode(new LyricsSnapshotDto { Title = "T", Language = "nl", Text = "x" }, Web)!.AsObject();

        (input.ContainsKey("expectedRevision"), plain.ContainsKey("assistedLineCount")).ShouldBe((false, false));
        (page["lyricsId"]!.GetValue<Guid>(), snapshot["assistedLineCount"]!.GetValue<int>()).ShouldBe((SheetId, 2));
        SchemaValidator.Validate(Backend, Schema("LyricsInput"), JsonSerializer.SerializeToNode(input.Deserialize<LyricsInput>(Web)! with { ExpectedRevision = 4 }, Web)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("{}", false, false)]
    [InlineData("{\"rhythmTarget\":null}", true, false)]
    [InlineData("{\"sensitivity\":\"strict\",\"rhythmTarget\":{\"bpm\":96,\"beatsPerBar\":4,\"barsPerLine\":2,\"syllablesPerBeat\":1.5}}", true, true)]
    public void SheetSettingsUpdate_TellsAbsentFromNull(string json, bool specified, bool hasTarget)
    {
        var update = JsonSerializer.Deserialize<SheetAssistSettingsUpdate>(json, Web)!;

        (update.RhythmTargetSpecified, update.RhythmTarget is not null).ShouldBe((specified, hasTarget));
        JsonSerializer.SerializeToNode(update, Web)!.AsObject().ContainsKey("sensitivity").ShouldBe(update.Sensitivity is not null);
    }

    private static bool Matches(string template, string route)
    {
        var path = route.Split('?')[0].Split('/');
        var parts = template.Split('/');
        return parts.Length == path.Length && parts.Zip(path).All(pair => pair.First.StartsWith('{') || pair.First == pair.Second);
    }

    private static readonly RhythmTargetDto TargetSample = new() { Bpm = 96, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.25 };

    private static readonly OwnWorkOverrideDto OverrideSample = new()
    {
        StatementVersion = LyricAssistLimits.OwnWorkStatementVersion,
        ConfirmedBy = "Robin",
        ConfirmedAt = At,
        MatchedTrackId = ItemId,
        MatchedTrackTitle = "Our band song",
    };

    private static readonly LyricSuggestionDto SuggestionSample = Sample<LyricSuggestionDto>("suggestion");

    private static readonly LyricAssistStatusDto StatusSample = new()
    {
        Languages = [AssistLanguageDto.Nl, AssistLanguageDto.En],
        Dictionary = new LayerInfoDto { Layer = SuggestionSourceDto.Dictionary, Name = "OpenTaal (nl), SCOWL (en)", Available = true },
        Model = new LyricAssistModelStatusDto { Backend = AgentBackendDto.Ollama, Ready = true, ActiveModel = "qwen3.5:9b", UsingFallback = false, QualityNote = "Dutch grammar: local model below target." },
        ClaudeNoticeRequired = false,
        AssistActive = null,
    };

    private static readonly LyricAssistSettingsDto SettingsSample = new()
    {
        ClaudeNoticeAcknowledged = true,
        ClaudeNoticeAcknowledgedAt = At,
        ClaudeNoticeVersion = LyricAssistLimits.ClaudeNoticeVersion,
        ClaudeNoticeText = "Your lyrics are sent to Anthropic.",
        OwnWorkStatementVersion = LyricAssistLimits.OwnWorkStatementVersion,
        OwnWorkStatementText = "This text is my own work.",
    };

    private static readonly SheetAssistInfoDto SheetInfoSample = new()
    {
        Sensitivity = SensitivityDto.Strict,
        RhythmTarget = TargetSample,
        OpenSuggestions = 3,
        AssistedLines = [new AssistedLineDto { Line = 2, SuggestionId = ItemId }],
        IntentionalMarkCount = 1,
        CanUndo = true,
        OwnWorkOverride = OverrideSample,
    };

    private static readonly AcceptResultDto AcceptedSample = new()
    {
        LyricsId = SheetId,
        Revision = 5,
        Title = "Zomeravond",
        Text = "[verse]\nde zon",
        Assist = SheetInfoSample,
        Applied = [ItemId],
        Stale = [],
        Reopened = [ItemId],
    };

    private static readonly SyllableReportDto SyllablesSample = new()
    {
        Language = AssistLanguageDto.Nl,
        Lines =
        [
            new SyllableLineDto { Line = 1, Syllables = 0, IsMarker = true, Section = "verse", Deviates = false },
            new SyllableLineDto { Line = 2, Syllables = 9, IsMarker = false, Section = "verse", SectionMedian = 8, Target = 6, Deviates = true },
        ],
        TargetDurationSeconds = 5,
        WordsPerSecond = 1.2,
    };

    private static readonly LyricAssistSummaryDto SummarySample = Sample<LyricAssistSummaryDto>("summary");

    private static readonly LyricAssistDto AssistSample = Sample<LyricAssistDto>("assist");

    private static readonly (string Schema, object Dto)[] DtoTable =
    [
        ("LyricAssistStatus", StatusSample),
        ("LyricAssistSettings", SettingsSample),
        ("LyricAssistSettingsUpdate", new LyricAssistSettingsUpdate { AcknowledgeClaudeNotice = true }),
        ("CreateLyricAssistRequest", new CreateLyricAssistRequest { Kind = AssistKindDto.Check, ExpectedRevision = 3, Categories = [SuggestionCategoryDto.Spelling] }),
        ("CreateLyricAssistRequest", new CreateLyricAssistRequest { Kind = AssistKindDto.Alternatives, ExpectedRevision = 1, LineFrom = 2, LineTo = 2, Count = 3, Instruction = "warmer" }),
        ("LyricSuggestion", SuggestionSample),
        ("CategoryCounts", CategoryCountsDto.None),
        ("DiscardedCounts", DiscardedCountsDto.None),
        ("LyricAssistSummary", SummarySample),
        ("LyricAssist", AssistSample),
        ("SuggestionList", new SuggestionListDto { LyricsId = SheetId, Revision = 3, Suggestions = [SuggestionSample] }),
        ("SuggestionDecisionRequest", new SuggestionDecisionRequest { SuggestionIds = [ItemId], ExpectedRevision = 3 }),
        ("RevisionRequest", new RevisionRequest { ExpectedRevision = 4 }),
        ("SheetAssistInfo", SheetAssistInfoDto.Default),
        ("SheetAssistInfo", SheetInfoSample),
        ("SheetAssistSettingsUpdate", new SheetAssistSettingsUpdate { Sensitivity = SensitivityDto.Loose, RhythmTarget = TargetSample }),
        ("AcceptResult", AcceptedSample),
        ("OwnWorkConfirmRequest", new OwnWorkConfirmRequest { MatchedTrackId = ItemId, ConfirmedBy = "Robin", StatementVersion = LyricAssistLimits.OwnWorkStatementVersion, Confirm = true }),
        ("OwnWorkOverride", OverrideSample),
        ("IntentionalMarkInput", new IntentionalMarkInput { Scope = IntentionalScopeDto.Word, Text = "lope", Note = "dialect" }),
        ("MarkSuggestionIntentionalRequest", new MarkSuggestionIntentionalRequest { Scope = IntentionalScopeDto.Line, ExpectedRevision = 3 }),
        ("IntentionalMark", new IntentionalMarkDto { MarkId = ItemId, Scope = IntentionalScopeDto.Phrase, Text = "ik ga d'r heen", InText = true, CreatedAt = At }),
        ("LyricsRevisionSummary", new LyricsRevisionSummaryDto { Revision = 4, Cause = RevisionCauseDto.Accept, AcceptGroupId = ItemId, ChangedLines = 1, CreatedAt = At }),
        ("LyricsRevisionSummary", new LyricsRevisionSummaryDto { Revision = 1, Cause = RevisionCauseDto.Created, CreatedAt = At }),
        ("SyllableRequest", new SyllableRequest { Language = AssistLanguageDto.Nl, Text = "[verse]\nde zomer\n", RhythmTarget = TargetSample }),
        ("SyllableReport", SyllablesSample),
        ("RhymeResult", new RhymeResultDto { Word = "night", Language = AssistLanguageDto.En, Rhymes = [new RhymeDto { Word = "light", Kind = RhymeKindDto.Perfect, Syllables = 1 }], Source = "CMU Pronouncing Dictionary" }),
        ("LyricWordInput", new LyricWordInput { Language = AssistLanguageDto.Nl, Word = "Zomeravond" }),
        ("LyricWord", new LyricWordDto { WordId = ItemId, Language = AssistLanguageDto.En, Word = "yeahhh", CreatedAt = At }),
        ("LanguageGuessRequest", new LanguageGuessRequest { Text = "de zon gaat onder" }),
        ("LanguageGuess", new LanguageGuessDto { Language = AssistLanguageDto.Nl, Confidence = 0.82 }),
        ("LanguageGuess", new LanguageGuessDto { Confidence = 0.31 }),
        ("PageContext", new PageContextDto { Page = "lyrics", LyricsId = SheetId }),
        ("LyricsSnapshot", new LyricsSnapshotDto { LyricsId = SheetId, Title = "Zomeravond", Language = "nl", Text = "de zon", AssistedLineCount = 1 }),
    ];

    public static TheoryData<string, object> Dtos => new(DtoTable);

    private static T Sample<T>(string name) => Samples.Value[name].Deserialize<T>(Web)!;
}
