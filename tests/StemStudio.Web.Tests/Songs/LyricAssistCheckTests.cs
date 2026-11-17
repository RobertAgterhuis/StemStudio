using Bunit;
using StemStudio.Web.Tests.Fakes;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Experience;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T033: Check, streamed suggestions, highlights and decisions (FR-005 - FR-012, FR-024).</summary>
public sealed class LyricAssistCheckTests : LyricEditorTestContext
{
    private const string Fixed = "[verse]\nIk weet niet waar de wind gaat\nDe nacht zijn warm en stil";

    [Fact]
    public void Check_StreamsSuggestionsIntoTheList_WithTheirAttributes()
    {
        var page = Checked(ExperienceLevel.Expert, SpellingFix(), GrammarFix());

        var entry = page.Find(Id($"lyric-suggestion-{Spelling}"));
        (entry.GetAttribute("role"), entry.GetAttribute("data-category"), entry.GetAttribute("data-type"), entry.GetAttribute("data-severity"))
            .ShouldBe(("option", "spelling", "spelling.typo", "error"));
        (entry.GetAttribute("data-status"), entry.GetAttribute("data-source"), entry.GetAttribute("data-line")).ShouldBe(("open", "dictionary", "2"));
        page.Find(Id($"lyric-suggestion-original-{Spelling}")).TextContent.ShouldBe("wiet");
        page.Find(Id($"lyric-suggestion-proposal-{Spelling}")).TextContent.ShouldBe("weet");
        page.Find(Id($"lyric-suggestion-explanation-{Spelling}")).TextContent.ShouldBe("Uitleg bij de suggestie.");
        page.Find(Id($"lyric-suggestion-source-{Grammar}")).TextContent.ShouldBe("model: qwen3.5:9b");
        page.Find(Id("lyric-assist-list")).GetAttribute("role").ShouldBe("listbox");
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Check finished: 2 suggestions - 1 spelling, 1 grammar");
        Api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Check && r.LineFrom == null && r.Instruction == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Highlights_MarkTheSpans_WithAnUnderlineStylePerCategory()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix(), DeviceIdea());

        var spelling = page.Find(Id($"lyric-highlight-{Spelling}"));
        (spelling.TextContent, spelling.GetAttribute("data-category"), spelling.ClassList.Contains("ss-hl-spelling")).ShouldBe(("wiet", "spelling", true));
        page.Find(Id($"lyric-highlight-{Grammar}")).ClassList.ShouldContain("ss-hl-grammar");
        page.Find(Id($"lyric-highlight-{Device}")).TextContent.ShouldBe("warm");
        page.Find(".ss-lyric-overlay").GetAttribute("aria-hidden").ShouldBe("true");
    }

    [Fact]
    public void Highlight_OfTextThatChanged_IsNotDrawn()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix());

        page.Find(Id("lyrics-text-input")).Input("[verse]\nIk weet niet waar de wind gaat");

        page.FindAll(Id($"lyric-highlight-{Spelling}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_WithUnsavedChanges_SavesFirst()
    {
        Api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet(text: Fixed, revision: 4));
        var page = Open();
        await page.Find(Id("lyrics-text-input")).InputAsync(new() { Value = Fixed });

        await page.Find(Id("lyric-assist-check")).ClickAsync(new());

        Received.InOrder(() =>
        {
            Api.UpdateLyricsSheetAsync(LyricsId, Arg.Is<LyricsInput>(i => i.Text == Fixed && i.ExpectedRevision == 3), Arg.Any<CancellationToken>());
            Api.CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.ExpectedRevision == 4), Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public void Running_ShowsProgressAndCancel()
    {
        Api.StreamLyricAssistEventsAsync(AssistId, Arg.Any<int?>(), Arg.Any<CancellationToken>()).Returns(_ => Slow());
        Api.CancelLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Cancelled));
        var page = Open();

        page.Find(Id("lyric-assist-check")).Click();

        page.WaitForAssertion(() => page.Find(Id("lyric-assist-progress")).GetAttribute("aria-valuenow").ShouldBe("50"));
        page.Find(Id("lyric-assist-pane")).GetAttribute("data-state").ShouldBe("running");
        page.Find(Id("lyric-assist-check")).HasAttribute("disabled").ShouldBeTrue();
        page.Find(Id("lyric-assist-cancel")).Click();
        page.WaitForAssertion(() => page.FindAll(Id("lyric-assist-cancel")).ShouldBeEmpty());
        Api.Received(1).CancelLyricAssistAsync(AssistId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Accept_ChangesTheText_MarksTheLine_AndUndoRestores()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Fixed));
        Api.UndoLyricSuggestionsAsync(LyricsId, 4, Arg.Any<CancellationToken>()).Returns(Accepted(Text, revision: 5) with { Assist = SheetAssistInfoDto.Default });
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        Api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List(SpellingFix(SuggestionStatusDto.Accepted), GrammarFix()));
        page.Find(Id("lyric-assist-undo")).HasAttribute("disabled").ShouldBeTrue();

        await page.Find(Id($"lyric-suggestion-accept-{Spelling}")).ClickAsync(new());

        page.Find(Id("lyrics-text-input")).GetAttribute("value").ShouldBe(Fixed);
        page.Find(Id("lyric-line-provenance-2")).GetAttribute("data-provenance").ShouldBe("accepted-suggestion");
        page.FindAll(Id($"lyric-suggestion-{Spelling}")).ShouldBeEmpty();
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Suggestion accepted");

        Api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List(SpellingFix(), GrammarFix()));
        await page.Find(Id("lyric-assist-undo")).ClickAsync(new());

        page.Find(Id("lyrics-text-input")).GetAttribute("value").ShouldBe(Text);
        page.FindAll(Id("lyric-line-provenance-2")).ShouldBeEmpty();
        page.FindAll(Id($"lyric-suggestion-{Spelling}")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task Reject_ClosesTheSuggestion()
    {
        Api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List(GrammarFix() with { Status = SuggestionStatusDto.Rejected }));
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        await page.Find(Id($"lyric-suggestion-reject-{Grammar}")).ClickAsync(new());

        page.FindAll(Id($"lyric-suggestion-{Grammar}")).ShouldBeEmpty();
        await Api.Received(1).RejectLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.SuggestionIds.Single() == Grammar && r.ExpectedRevision == 3), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MarkIntentional_UsesTheChosenScope_AndIsOnlyOfferedForCorrections()
    {
        Api.MarkSuggestionIntentionalAsync(LyricsId, Grammar, Arg.Any<MarkSuggestionIntentionalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IntentionalMarkDto { MarkId = Guid.NewGuid(), Scope = IntentionalScopeDto.Line, Text = "De nacht zijn warm en stil", InText = true, CreatedAt = DateTimeOffset.UnixEpoch });
        var page = Checked(ExperienceLevel.Advanced, GrammarFix(), DeviceIdea());
        page.FindAll(Id($"lyric-suggestion-intentional-{Device}")).ShouldBeEmpty();

        await page.Find(Id($"lyric-suggestion-intentional-scope-{Grammar}")).ChangeAsync(new() { Value = "Line" });
        await page.Find(Id($"lyric-suggestion-intentional-{Grammar}")).ClickAsync(new());

        await Api.Received(1).MarkSuggestionIntentionalAsync(LyricsId, Grammar, Arg.Is<MarkSuggestionIntentionalRequest>(r => r.Scope == IntentionalScopeDto.Line), Arg.Any<CancellationToken>());
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Marked as intentional: De nacht zijn warm en stil");
    }

    [Fact]
    public void Beginner_MarksWithTheDefaultScope_WithoutTheMenu()
    {
        Api.MarkSuggestionIntentionalAsync(LyricsId, Spelling, Arg.Any<MarkSuggestionIntentionalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IntentionalMarkDto { MarkId = Guid.NewGuid(), Scope = IntentionalScopeDto.Word, Text = "wiet", InText = true, CreatedAt = DateTimeOffset.UnixEpoch });
        var page = Checked(ExperienceLevel.Beginner, SpellingFix());

        page.FindAll(Id($"lyric-suggestion-intentional-scope-{Spelling}")).ShouldBeEmpty();
        page.FindAll(Id($"lyric-suggestion-add-word-{Spelling}")).ShouldBeEmpty();
        page.Find(Id($"lyric-suggestion-intentional-{Spelling}")).Click();

        page.WaitForAssertion(() => Api.Received(1).MarkSuggestionIntentionalAsync(LyricsId, Spelling, Arg.Is<MarkSuggestionIntentionalRequest>(r => r.Scope == IntentionalScopeDto.Word), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public async Task AddToMyWords_IsOfferedForSpellingOnly()
    {
        Api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List(SpellingFix(SuggestionStatusDto.Rejected)));
        var page = Checked(ExperienceLevel.Advanced, SpellingFix(), GrammarFix());
        page.FindAll(Id($"lyric-suggestion-add-word-{Grammar}")).ShouldBeEmpty();

        await page.Find(Id($"lyric-suggestion-add-word-{Spelling}")).ClickAsync(new());

        await Api.Received(1).AddLyricWordAsync(Arg.Is<LyricWordInput>(w => w.Word == "wiet"), Arg.Any<CancellationToken>());
        page.FindAll(Id($"lyric-suggestion-{Spelling}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Filters_ShowOneCategory_AndAcceptAllTakesExactlyTheVisibleOnes()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Fixed));
        var page = Checked(ExperienceLevel.Advanced, SpellingFix(), GrammarFix(), DeviceIdea());
        page.Find(Id("lyric-assist-accept-all")).TextContent.Trim().ShouldBe("Accept all (3)");

        await page.Find(Id("lyric-assist-filter-spelling")).ClickAsync(new());

        page.Find(Id("lyric-assist-filter-spelling")).GetAttribute("aria-pressed").ShouldBe("true");
        page.Find(Id("lyric-assist-filter-all")).GetAttribute("aria-pressed").ShouldBe("false");
        page.FindAll("[role=option]").Select(o => o.GetAttribute("data-category")).ShouldBe(["spelling"]);
        page.Find(Id("lyric-assist-accept-all")).TextContent.Trim().ShouldBe("Accept all (1)");
        await page.Find(Id("lyric-assist-accept-all")).ClickAsync(new());

        await Api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.SuggestionIds.SequenceEqual(new[] { Spelling })), Arg.Any<CancellationToken>());
        await page.Find(Id("lyric-assist-filter-all")).ClickAsync(new());
        page.FindAll("[role=option]").Count.ShouldBe(3);
    }

    [Fact]
    public async Task StaleRevision_ReloadsTheSheet()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<AcceptResultDto>>(_ => throw LyricAssistData.Problem(LyricAssistProblemTypes.RevisionConflict, """{"currentRevision":7}"""));
        var page = Checked(ExperienceLevel.Beginner, SpellingFix());
        Api.GetLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>()).Returns(Sheet(text: Fixed, revision: 7));

        await page.Find(Id($"lyric-suggestion-accept-{Spelling}")).ClickAsync(new());

        page.Find(Id("lyrics-text-input")).GetAttribute("value").ShouldBe(Fixed);
        page.Find(Id("lyric-assist-error")).TextContent.ShouldStartWith("The lyrics were changed elsewhere");
    }

    [Fact]
    public async Task Sensitivity_IsStoredPerSheet()
    {
        Api.UpdateSheetAssistSettingsAsync(LyricsId, Arg.Any<SheetAssistSettingsUpdate>(), Arg.Any<CancellationToken>()).Returns(SheetAssistInfoDto.Default with { Sensitivity = SensitivityDto.Strict });
        var page = Open(ExperienceLevel.Advanced);
        page.Find(Id("lyric-assist-sensitivity-balanced")).HasAttribute("checked").ShouldBeTrue();

        await page.Find(Id("lyric-assist-sensitivity-strict")).ChangeAsync(new() { Value = "on" });

        await Api.Received(1).UpdateSheetAssistSettingsAsync(LyricsId, Arg.Is<SheetAssistSettingsUpdate>(u => u.Sensitivity == SensitivityDto.Strict && u.RhythmTargetSpecified), Arg.Any<CancellationToken>());
        page.Find(Id("lyric-assist-sensitivity-strict")).HasAttribute("checked").ShouldBeTrue();
    }

    [Fact]
    public async Task MarksAndWords_AreListed_AndRemovable()
    {
        var markId = Guid.NewGuid();
        var wordId = Guid.NewGuid();
        Api.ListIntentionalMarksAsync(LyricsId, Arg.Any<CancellationToken>()).Returns([new IntentionalMarkDto { MarkId = markId, Scope = IntentionalScopeDto.Phrase, Text = "Gij zijt", InText = false, CreatedAt = DateTimeOffset.UnixEpoch }]);
        Api.ListLyricWordsAsync(AssistLanguageDto.Nl, Arg.Any<CancellationToken>()).Returns([new LyricWordDto { WordId = wordId, Language = AssistLanguageDto.Nl, Word = "Zeeland", CreatedAt = DateTimeOffset.UnixEpoch }]);
        EditorInterop.GetSelectionAsync(Arg.Any<Microsoft.AspNetCore.Components.ElementReference>()).Returns((11, 15));
        var page = Open(ExperienceLevel.Advanced);

        await page.Find(Id("lyric-marks-open")).ClickAsync(new());
        page.Find(Id($"lyric-mark-{markId}")).TextContent.ShouldContain("no longer in the text");
        await page.Find(Id("lyric-mark-selection")).ClickAsync(new());
        await page.Find(Id($"lyric-mark-delete-{markId}")).ClickAsync(new());
        await page.Find(Id("lyric-words-open")).ClickAsync(new());
        page.Find(Id("lyric-words-list")).TextContent.ShouldContain("Zeeland");
        await page.Find(Id($"lyric-word-delete-{wordId}")).ClickAsync(new());

        await Api.Received(1).CreateIntentionalMarkAsync(LyricsId, Arg.Is<IntentionalMarkInput>(m => m.Text == "wiet" && m.Scope == IntentionalScopeDto.Word), Arg.Any<CancellationToken>());
        await Api.Received(1).DeleteIntentionalMarkAsync(LyricsId, markId, Arg.Any<CancellationToken>());
        await Api.Received(1).DeleteLyricWordAsync(wordId, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void SheetText_IsRenderedAsText_NeverAsMarkup()
    {
        var hostile = GrammarFix() with { Explanation = "<img src=x onerror=alert(1)>", Proposal = "<b>vet</b>" };

        var page = Checked(ExperienceLevel.Beginner, hostile);

        page.FindAll("img").ShouldBeEmpty();
        page.FindAll(".ss-assist b").ShouldBeEmpty();
        page.Find(Id($"lyric-suggestion-explanation-{Grammar}")).TextContent.ShouldBe("<img src=x onerror=alert(1)>");
    }

    private static async IAsyncEnumerable<StemStudio.Web.Components.Lyrics.Assist.LyricAssistEvent> Slow()
    {
        await foreach (var e in Events((LyricAssistEventTypes.Progress, new LyricAssistProgressEventDto { BlocksDone = 1, BlocksSkipped = 0, BlocksTotal = 2 })))
        {
            yield return e;
        }

        await Task.Delay(Timeout.Infinite, Xunit.TestContext.Current.CancellationToken);
    }
}
