using System.Text.Json.Nodes;
using StemStudio.Contracts;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using static StemStudio.Api.Tests.Contract.ContractJson;

namespace StemStudio.Api.Tests.Contract;

/// <summary>
/// Skeleton tests (architect, 023 Phase 0, T001/T005): backend-api 1.23.0
/// (<c>specs/023-lyric-writing-assistant/contracts/backend-api.openapi.yaml</c>) is 022's full 1.22.0 (012's 1.11.0 in the 023 Phase 0) plus exactly the standalone
/// 023 delta (<c>backend-api.delta.openapi.yaml</c>, 1.23.0-delta.1) and the "schema additions at the merge" (DELTA.md section 1),
/// and the lyric-assistant enums, problem codes, limits and routes match it.
/// </summary>
public sealed partial class LyricAssistContractTests
{
    private static readonly Lazy<OpenApiContract> LazyDelta = new(() =>
        OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "023", "backend-api.delta.openapi.yaml")));

    // Base components the delta repeats so it validates on its own; the merged document keeps their 1.11.0 definitions.
    private static readonly string[] SharedWith1110 = ["LyricsId", "NotFound", "ValidationProblem", "ProblemDetails", "ValidationProblemDetails", "TokenUsage"];

    private static OpenApiContract Delta => LazyDelta.Value;

    private static OpenApiContract Backend => OpenApiContract.Backend;

    // The full document 1.23.0 was merged onto: 022's 1.22.0 (= 013's 1.12.0 + 022; 012's 1.11.0 in the 023 Phase 0), re-pointed at
    // the 022+023 round start (DELTA.md section 5, merge_full.py); every assertion below proves "base + exactly the 023 delta".
    private static OpenApiContract BaseDocument => Backend022;

    // DELTA.md section 1 "Schema additions at the merge": the only 1.11.0 entries 1.23.0 changes.
    public static TheoryData<string, string[]> Changed1110Entries => new()
    {
        { "paths", [] },
        { "components/schemas", ["LyricsSheet", "LyricsInput", "LyricsSnapshot", "PageContext", "JobPhase", "DecisionReason"] },
        { "components/responses", [] },
        { "components/examples", ["LyricsSheetExample"] },
        { "components/parameters", [] },
        { "components/headers", [] },
    };

    public static TheoryData<string> DeltaSections => new("paths", "components/parameters", "components/responses", "components/schemas");

    [Fact]
    public void Backend_IsLabelled1230AndListsTheMergedDeltas()
    {
        var info = Backend.Root["info"]!;

        info["version"]!.GetValue<string>().ShouldBe("1.23.0");
        Strings(info["x-stemstudio-contains"]!).ShouldBe(["1.1.0", "1.2.0", "1.3.0", "1.4.0", "1.5.0", "1.6.0", "1.7.0", "1.8.0", "1.9.0", "1.10.0", "1.21.0", "1.11.0", "1.12.0", "1.22.0", "1.23.0"]);
        Delta.Root["info"]!["version"]!.GetValue<string>().ShouldBe("1.23.0-delta.1");
    }

    [Fact]
    public void Backend1230_Is1110PlusExactlyTheDeltaOperations()
    {
        Backend.Operations.Select(Signature).ShouldBe(BaseDocument.Operations.Concat(Delta.Operations).Select(Signature), ignoreOrder: true);
        Delta.Operations.ShouldAllBe(op => op.Path.StartsWith(ApiRoutes.LyricAssist + "/", StringComparison.Ordinal)
            || op.Path.StartsWith(ApiRoutes.LyricAssists + "/", StringComparison.Ordinal)
            || op.Path.StartsWith(ApiRoutes.LyricsSheets + "/{lyricsId}/", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(Changed1110Entries))]
    public void Backend1230_Contains1110UnchangedExceptTheNamedEntries(string section, string[] changed)
    {
        ChangedEntries(BaseDocument, Backend, section).ShouldBe(changed, ignoreOrder: true);
    }

    [Theory]
    [MemberData(nameof(DeltaSections))]
    public void Backend1230_ContainsTheDeltaUnchangedExceptTheSharedBaseComponents(string section)
    {
        ChangedEntries(Delta, Backend, section).ShouldBeSubsetOf(SharedWith1110);
        Keys(Delta, section).Where(SharedWith1110.Contains)
            .ShouldAllBe(key => JsonNode.DeepEquals(Section(BaseDocument.Root, section)[key], Section(Backend.Root, section)[key]));
    }

    [Theory]
    [MemberData(nameof(DeltaSections))]
    public void Backend1230_AddsNothingButTheDeltaEntries(string section)
    {
        Keys(Backend, section).ShouldBe(Keys(BaseDocument, section).Union(Keys(Delta, section)), ignoreOrder: true);
    }

    [Theory]
    [InlineData("LyricsSheet", new[] { "revision", "assist" })]
    [InlineData("LyricsInput", new[] { "expectedRevision" })]
    [InlineData("LyricsSnapshot", new[] { "assistedLineCount" })]
    [InlineData("PageContext", new[] { "lyricsId" })]
    public void ChangedSchemas_OnlyGainTheirNewMembers(string schema, string[] added)
    {
        var before = Section(BaseDocument.Root, "components/schemas")[schema]!["properties"]!.AsObject();
        var after = Schema(schema)["properties"]!.AsObject();

        after.Select(p => p.Key).Except(before.Select(p => p.Key)).ShouldBe(added, ignoreOrder: true);
        before.ShouldAllBe(p => JsonNode.DeepEquals(p.Value, after[p.Key]));
    }

    [Theory]
    [InlineData("JobPhase", "lyricAssist")]
    [InlineData("DecisionReason", "lyrics-edit")]
    public void ChangedEnums_AppendOneValue(string schema, string value)
    {
        var before = Strings(Section(BaseDocument.Root, "components/schemas")[schema]!["enum"]!);

        Strings(Schema(schema)["enum"]!).ShouldBe([.. before, value]);
    }

    [Fact]
    public void LyricsSheet_RequiresTheNewResponseMembersOnly()
    {
        var before = Strings(Section(BaseDocument.Root, "components/schemas")["LyricsSheet"]!["required"]!);

        Strings(Schema("LyricsSheet")["required"]!).ShouldBe([.. before, "revision", "assist"]);
        Strings(Schema("LyricsInput")["required"]!).ShouldNotContain("expectedRevision");
    }

    [Fact]
    public void Enums_MatchTheContract() => AssertEnumsMatch(
    [
        (typeof(AssistLanguageDto), "AssistLanguage"), (typeof(SensitivityDto), "Sensitivity"), (typeof(AssistKindDto), "AssistKind"),
        (typeof(SuggestionCategoryDto), "SuggestionCategory"), (typeof(SuggestionTypeDto), "SuggestionType"),
        (typeof(SuggestionSeverityDto), "SuggestionSeverity"), (typeof(SuggestionStatusDto), "SuggestionStatus"),
        (typeof(SuggestionSourceDto), "SuggestionSource"), (typeof(SuggestionOperationDto), "SuggestionOperation"),
        (typeof(AssistStatusDto), "AssistStatus"), (typeof(IntentionalScopeDto), "IntentionalScope"), (typeof(JobPhaseDto), "JobPhase"),
    ]);

    [Theory]
    [InlineData(typeof(SuggestionSourceDto), "LayerInfo", "layer")]
    [InlineData(typeof(AssistOriginDto), "LyricAssistSummary", "origin")]
    [InlineData(typeof(RevisionCauseDto), "LyricsRevisionSummary", "cause")]
    [InlineData(typeof(RhymeKindDto), "Rhyme", "kind")]
    public void InlineEnums_MatchTheContract(Type enumType, string schema, string property)
    {
        WireNames(enumType).ShouldBe(Strings(Schema(schema)["properties"]![property]!["enum"]!));
    }

    [Fact]
    public void NestedEnums_MatchTheContract()
    {
        var failure = Schema("LyricAssistSummary")["properties"]!["failure"]!["oneOf"]![1]!["properties"]!["code"]!;
        var model = Schema("LyricAssistStatus")["properties"]!["model"]!["properties"]!["backend"]!;

        WireNames(typeof(AssistFailureCodeDto)).ShouldBe(Strings(failure["enum"]!));
        WireNames(typeof(AgentBackendDto)).ShouldBe(Strings(model["enum"]!));
        WireNames(typeof(AssistNoteCodeDto)).ShouldBe(Strings(EventVariant("assist.note")["properties"]!["code"]!["enum"]!));
    }

    [Fact]
    public void DecisionReasons_IncludeLyricsEdit()
    {
        Strings(Schema("DecisionReason")["enum"]!).ShouldContain(AgentDecisionReasons.LyricsEdit);
        AgentDecisionReasons.LyricsEdit.ShouldBe("lyrics-edit");
    }

    [Fact]
    public void ProblemCodes_AndMembers_MatchAssistProblem()
    {
        var extension = Schema("AssistProblem")["allOf"]![1]!["properties"]!.AsObject();

        LyricAssistProblemTypes.AssistProblemCodes.ShouldBe(Strings(extension["code"]!["enum"]!));
        ConstantValues(typeof(LyricAssistProblemMembers)).ShouldBe(extension.Select(p => p.Key), ignoreOrder: true);
    }

    [Theory]
    [InlineData("ValidationOrImitation", LyricAssistProblemTypes.Imitation, 400)]
    [InlineData("AssistConflict", LyricAssistProblemTypes.NotOwnLyrics, 409)]
    public void ProblemTypes_MatchTheResponseExamples(string response, string code, int status)
    {
        var example = Section(Backend.Root, "components/responses")[response]!["content"]!["application/problem+json"]!["example"]!;

        (example["type"]!.GetValue<string>(), (int)example["status"]!.GetValue<double>()).ShouldBe((LyricAssistProblemTypes.Uri(code), status));
    }

    [Theory]
    [InlineData("AssistEnded", LyricAssistProblemTypes.AssistEnded)]
    [InlineData("TooLong", LyricAssistProblemTypes.TooLong)]
    [InlineData("MarkLimit", LyricAssistProblemTypes.MarkLimit)]
    [InlineData("WordLimit", LyricAssistProblemTypes.WordLimit)]
    public void PlainProblemCodes_AreNamedByTheirResponses(string response, string code)
    {
        Section(Backend.Root, "components/responses")[response]!["description"]!.GetValue<string>().ShouldStartWith($"`{code}`");
    }

    [Fact]
    public void EventTypes_MatchTheEventVariants()
    {
        var titles = Schema("LyricAssistEvent")["anyOf"]!.AsArray().Select(variant => variant!["title"]!.GetValue<string>());

        LyricAssistEventTypes.All.ShouldBe(titles);
        LyricAssistEventTypes.All.Where(LyricAssistEventTypes.IsTerminal).ShouldBe(["assist.completed", "assist.failed", "assist.cancelled"]);
    }

    private static JsonNode EventVariant(string title) =>
        Schema("LyricAssistEvent")["anyOf"]!.AsArray().Single(variant => variant!["title"]!.GetValue<string>() == title)!;
}
