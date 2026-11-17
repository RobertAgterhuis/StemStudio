using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Agent;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Agent;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Components.Agent;

/// <summary>023 T058: the approval card of <c>apply_lyric_suggestions</c> renders before -> after from the STORED suggestions (FR-018).</summary>
public sealed class LyricsApprovalDiffTests : PageTestContext
{
    private static string Args(params Guid[] ids) =>
        $$"""{"lyricsId":"{{LyricsId}}","suggestionIds":[{{string.Join(',', ids.Select(i => $"\"{i}\""))}}]}""";

    private IRenderedComponent<LyricsApprovalDiff> Diff(string arguments) =>
        Render<LyricsApprovalDiff>(p => p.Add(x => x.Arguments, AgentData.Json(arguments)));

    [Fact]
    public void Rows_ComeFromTheStoredSuggestions_InTextOrder()
    {
        Api.ListLyricSuggestionsAsync(LyricsId, SuggestionStatusDto.Open, Arg.Any<CancellationToken>()).Returns(List(GrammarFix(), SpellingFix(), DeviceIdea()));

        var cut = Diff(Args(Grammar, Spelling));

        cut.WaitForElement($"[data-testid=agent-approval-lyrics-diff-{Spelling}]");
        cut.FindAll("[data-testid^=agent-approval-lyrics-diff-]").Select(r => r.GetAttribute("data-testid"))
            .ShouldBe([$"agent-approval-lyrics-diff-{Spelling}", $"agent-approval-lyrics-diff-{Grammar}"]);
        var row = cut.Find($"[data-testid=agent-approval-lyrics-diff-{Spelling}]");
        (row.QuerySelector("del")!.TextContent, row.QuerySelector("ins")!.TextContent).ShouldBe(("wiet", "weet"));
        cut.FindAll($"[data-testid=agent-approval-lyrics-diff-{Device}]").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("no longer open");
    }

    [Fact]
    public void ModelArguments_CarryNoText_AndUnknownIdsAreCounted()
    {
        Api.ListLyricSuggestionsAsync(LyricsId, SuggestionStatusDto.Open, Arg.Any<CancellationToken>()).Returns(List(SpellingFix()));

        var cut = Diff($$"""{"lyricsId":"{{LyricsId}}","suggestionIds":["{{Spelling}}","{{Guid.NewGuid()}}","not-a-guid",7],"text":"<script>x</script>"}""");

        cut.WaitForAssertion(() => cut.Markup.ShouldContain("1 of the requested suggestions are no longer open"));
        cut.Markup.ShouldNotContain("<script>");
    }

    [Fact]
    public void TitleRow_IsNamedTitle_AndInsertionsShowTheNewLines()
    {
        var title = Suggestion(Guid.NewGuid(), SuggestionCategoryDto.Title, SuggestionTypeDto.WriteTitle, new AnchorDto { Line = 0, Start = 0, Length = 0, Original = "Zomeravond" }, "Zomernacht");
        var cont = Suggestion(Guid.NewGuid(), SuggestionCategoryDto.Continuation, SuggestionTypeDto.WriteContinuation, new AnchorDto { Line = 3, Start = 0, Length = 0, Original = string.Empty }, null);
        Api.ListLyricSuggestionsAsync(LyricsId, SuggestionStatusDto.Open, Arg.Any<CancellationToken>()).Returns(List(title, cont));

        var cut = Diff(Args(title.SuggestionId, cont.SuggestionId));

        cut.WaitForAssertion(() => cut.Find($"[data-testid=agent-approval-lyrics-diff-{title.SuggestionId}] td").TextContent.ShouldBe("Title"));
        cut.Find($"[data-testid=agent-approval-lyrics-diff-{cont.SuggestionId}] ins").TextContent.ShouldBeEmpty();
    }

    [Fact]
    public void WithoutASheet_SaysSo_AndAsksNothing()
    {
        var cut = Diff("""{"suggestionIds":[]}""");

        cut.Find("[role=alert]").TextContent.ShouldBe("The request names no lyrics sheet.");
        Api.DidNotReceive().ListLyricSuggestionsAsync(Arg.Any<Guid>(), Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void ApiError_IsShown()
    {
        Api.ListLyricSuggestionsAsync(LyricsId, SuggestionStatusDto.Open, Arg.Any<CancellationToken>()).Returns<Task<SuggestionListDto>>(_ => throw new ApiException(ApiError.Unreachable));

        var cut = Diff(Args(Spelling));

        cut.WaitForAssertion(() => cut.Find("[role=alert]").TextContent.ShouldBe(ApiError.Unreachable.UserMessage));
    }

    [Fact]
    public void ApprovalCard_ShowsTheDiff_AndTheLyricsReason()
    {
        Api.ListLyricSuggestionsAsync(LyricsId, SuggestionStatusDto.Open, Arg.Any<CancellationToken>()).Returns(List(SpellingFix()));
        var cut = RenderAtLevel<AgentPanel>(StemStudio.Web.Experience.ExperienceLevel.Advanced, _ => { });
        cut.WaitForAssertion(() => cut.Find("[data-testid=agent-input]").HasAttribute("disabled").ShouldBeFalse());
        cut.Find("[data-testid=agent-input]").Input("accept the spelling fixes");
        cut.Find("[data-testid=agent-send]").Click();

        AgentEvents.Started().Push(AgentEventTypes.ApprovalRequested, AgentData.Approval(ToolRiskDto.Change, reasons: [AgentDecisionReasons.LyricsEdit]) with
        {
            Tool = LyricsApprovalDiff.Tool,
            Arguments = AgentData.Json(Args(Spelling)),
            Effect = "Applies 1 suggestion to Zomeravond",
        });

        cut.WaitForElement($"[data-testid=agent-approval-lyrics-diff-{Spelling}]");
        cut.Find("[data-testid=agent-approval-card]").TextContent.ShouldContain("Changes the text of your lyrics: check every change below");
        cut.Find("[data-testid=agent-approval-card]").QuerySelector("[data-testid=agent-approval-lyrics-diff]").ShouldNotBeNull();
    }

    [Fact]
    public void NonObjectArguments_ReadAsEmpty()
    {
        var (sheet, ids) = LyricsDiffArguments.Read(AgentData.Json("[]"));

        (sheet, ids.Count).ShouldBe((null, 0));
    }
}
