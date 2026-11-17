using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Experience;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T035: keyboard and screen reader (FR-028, SC-008; ui-test-ids 1.23.0 "Keyboard" and announcements).</summary>
public sealed class LyricAssistKeyboardTests : LyricEditorTestContext
{
    private static KeyboardEventArgs Key(string key, bool alt = false, bool ctrl = false, bool shift = false) =>
        new() { Key = key, AltKey = alt, CtrlKey = ctrl, ShiftKey = shift };

    [Fact]
    public void F8_InTheEditor_FocusesTheNextSuggestion_MovesTheCursor_AndAnnounces()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        page.Find(".ss-lyrics-workspace").KeyDown(Key("F8"));

        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Spelling}");
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Line 2, spelling error: wiet, proposed weet. Uitleg bij de suggestie.");
        EditorInterop.Received(1).SelectAsync(Arg.Any<ElementReference>(), 11, 15);
        EditorInterop.Received(1).FocusAsync(SuggestionEntry.EntryId(Spelling));
        page.Find($"[data-testid=lyric-highlight-{Spelling}]").ClassList.ShouldContain("is-active");
    }

    [Fact]
    public void ShiftF8_GoesBack_AndWraps()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        page.Find(".ss-lyrics-workspace").KeyDown(Key("F8", shift: true));

        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Grammar}");
    }

    [Fact]
    public void F8_InTheList_MovesWithinTheList_Once()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        var list = page.Find(Id("lyric-assist-list"));

        list.KeyDown(Key("F8"));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));

        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Grammar}");
        EditorInterop.Received(1).FocusAsync(SuggestionEntry.EntryId(Grammar));
    }

    [Fact]
    public void TabbingIntoAnEntry_MakesItTheFocusedSuggestion_WithoutMovingTheCursor()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        EditorInterop.ClearReceivedCalls();

        page.Find(Id($"lyric-suggestion-reject-{Grammar}")).FocusIn();
        page.Find(Id($"lyric-suggestion-reject-{Grammar}")).FocusIn();

        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Grammar}");
        page.Find(Id($"lyric-highlight-{Grammar}")).ClassList.ShouldContain("is-active");
        EditorInterop.DidNotReceive().SelectAsync(Arg.Any<ElementReference>(), Arg.Any<int>(), Arg.Any<int>());
        EditorInterop.DidNotReceive().FocusAsync(Arg.Any<string>());
    }

    [Fact]
    public void DecisionKeys_WithoutAFocusedEntry_DoNothing_AndShiftF8InTheList_GoesBack()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        page.Find(Id("lyric-assist-list")).KeyDown(Key("Enter", alt: true));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8", shift: true));

        Api.DidNotReceive().AcceptLyricSuggestionsAsync(Arg.Any<Guid>(), Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>());
        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Grammar}");
    }

    [Fact]
    public void AltEnter_MovesTheFocus_ToTheEntryThatTakesThePlaceOfTheAcceptedOne()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Text));
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        Api.ListLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionStatusDto?>(), Arg.Any<CancellationToken>()).Returns(List(SpellingFix(SuggestionStatusDto.Accepted), GrammarFix()));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));
        EditorInterop.ClearReceivedCalls();

        page.Find(Id("lyric-assist-list")).KeyDown(Key("Enter", alt: true));

        page.WaitForAssertion(() => ActiveEntry(page).ShouldBe($"lyric-suggestion-{Grammar}"));
        EditorInterop.Received(1).FocusAsync(SuggestionEntry.EntryId(Grammar));
        page.Find(Id("lyric-assist-live")).TextContent.ShouldNotStartWith("Line");
    }

    [Fact]
    public async Task RejectButton_MovesTheFocus_ToTheNextEntry_AndTheLastDecisionLeavesTheFocusAlone()
    {
        Api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>())
            .Returns(List(GrammarFix() with { Status = SuggestionStatusDto.Rejected }), List(SpellingFix() with { Status = SuggestionStatusDto.Rejected }));
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        EditorInterop.ClearReceivedCalls();

        await page.Find(Id($"lyric-suggestion-reject-{Grammar}")).ClickAsync(new());

        ActiveEntry(page).ShouldBe($"lyric-suggestion-{Spelling}");
        await EditorInterop.Received(1).FocusAsync(SuggestionEntry.EntryId(Spelling));
        EditorInterop.ClearReceivedCalls();
        await page.Find(Id($"lyric-suggestion-reject-{Spelling}")).ClickAsync(new());

        page.FindAll(Id("lyric-assist-list")).ShouldBeEmpty();
        await EditorInterop.DidNotReceive().FocusAsync(Arg.Any<string>());
    }

    [Fact]
    public async Task AFailedDecision_KeepsTheEntryAndTheFocus()
    {
        Api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List());
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        EditorInterop.ClearReceivedCalls();

        await page.Find(Id($"lyric-suggestion-reject-{Spelling}")).ClickAsync(new());

        page.FindAll(Id($"lyric-suggestion-{Spelling}")).Count.ShouldBe(1);
        await EditorInterop.DidNotReceive().FocusAsync(Arg.Any<string>());
    }

    [Fact]
    public void AltEnter_OnAButtonOfAnEntry_AcceptsThatEntry()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Text));
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        page.Find(Id($"lyric-suggestion-reject-{Grammar}")).FocusIn();

        page.Find(Id($"lyric-suggestion-reject-{Grammar}")).KeyDown(Key("Enter", alt: true));

        page.WaitForAssertion(() => Api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.SuggestionIds.Single() == Grammar), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void AltEnter_Accepts_AltBackspace_Rejects()
    {
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Text));
        Api.RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(List());
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));

        page.Find(Id("lyric-assist-list")).KeyDown(Key("Enter", alt: true));
        page.WaitForAssertion(() => Api.Received(1).AcceptLyricSuggestionsAsync(LyricsId, Arg.Is<SuggestionDecisionRequest>(r => r.SuggestionIds.Single() == Spelling), Arg.Any<CancellationToken>()));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("Backspace", alt: true));

        page.WaitForAssertion(() => Api.Received(1).RejectLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void AltI_MarksTheFocusedCorrection_WithTheDefaultScope()
    {
        Api.MarkSuggestionIntentionalAsync(LyricsId, Grammar, Arg.Any<MarkSuggestionIntentionalRequest>(), Arg.Any<CancellationToken>())
            .Returns(new IntentionalMarkDto { MarkId = Guid.NewGuid(), Scope = IntentionalScopeDto.Phrase, Text = "zijn", InText = true, CreatedAt = DateTimeOffset.UnixEpoch });
        var page = Checked(ExperienceLevel.Beginner, GrammarFix(), DeviceIdea());
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));

        page.Find(Id("lyric-assist-list")).KeyDown(Key("i", alt: true));

        page.WaitForAssertion(() => Api.Received(1).MarkSuggestionIntentionalAsync(LyricsId, Grammar, Arg.Is<MarkSuggestionIntentionalRequest>(r => r.Scope == IntentionalScopeDto.Phrase), Arg.Any<CancellationToken>()));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("I", alt: true));
        Api.DidNotReceive().MarkSuggestionIntentionalAsync(LyricsId, Device, Arg.Any<MarkSuggestionIntentionalRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Escape_GoesBackToTheText_AtTheSuggestion()
    {
        var page = Checked(ExperienceLevel.Beginner, GrammarFix());
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));
        EditorInterop.ClearReceivedCalls();

        page.Find(Id("lyric-assist-list")).KeyDown(Key("Escape"));

        EditorInterop.Received(1).SelectAsync(Arg.Any<ElementReference>(), 48, 52);
        EditorInterop.DidNotReceive().FocusAsync(Arg.Any<string>());
    }

    [Fact]
    public void CtrlAltC_Checks()
    {
        var page = Open(ExperienceLevel.Beginner);

        page.Find(".ss-lyrics-workspace").KeyDown(Key("c", alt: true, ctrl: true));

        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId, Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Check), Arg.Any<CancellationToken>()));
        page.Find(Id("lyric-assist-live")).TextContent.ShouldNotBeEmpty();
    }

    [Fact]
    public void CtrlAltC_ForAnUnsupportedLanguage_DoesNothing()
    {
        var page = Open(ExperienceLevel.Beginner, Sheet("de"));

        page.Find(".ss-lyrics-workspace").KeyDown(Key("C", alt: true, ctrl: true));
        page.Find(".ss-lyrics-workspace").KeyDown(Key("x"));

        Api.DidNotReceive().CreateLyricAssistAsync(Arg.Any<Guid>(), Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Keys_WithoutSuggestions_DoNothing()
    {
        var page = Open(ExperienceLevel.Beginner);

        page.Find(".ss-lyrics-workspace").KeyDown(Key("F8"));

        EditorInterop.DidNotReceive().SelectAsync(Arg.Any<ElementReference>(), Arg.Any<int>(), Arg.Any<int>());
    }

    [Fact]
    public void Page_BindsTheAssistKeys_InTheBrowser()
    {
        Open(ExperienceLevel.Beginner);

        EditorInterop.Received(1).BindKeysAsync(Arg.Any<ElementReference>());
    }

    [Fact]
    public void TheList_IsAPlainList_OfListItems_WithoutListboxSemantics()
    {
        var page = Checked(ExperienceLevel.Expert, SpellingFix(), GrammarFix(), DeviceIdea() with { Proposal = null });

        var list = page.Find(Id("lyric-assist-list"));
        (list.TagName, list.GetAttribute("role"), list.GetAttribute("tabindex"), list.GetAttribute("aria-activedescendant")).ShouldBe(("UL", "list", null, null));
        list.Children.Length.ShouldBe(3);
        foreach (var entry in list.Children)
        {
            (entry.TagName, entry.GetAttribute("role"), entry.GetAttribute("tabindex")).ShouldBe(("LI", "listitem", "-1"), entry.OuterHtml);
            (entry.GetAttribute("aria-label"), entry.GetAttribute("aria-labelledby"), entry.GetAttribute("aria-selected")).ShouldBe((null, null, null), entry.OuterHtml);
        }
        page.Find(Id("lyric-assist-live")).GetAttribute("aria-live").ShouldBe("polite");
    }

    [Fact]
    public void TheEntries_HoldNoInteractiveRole_AroundTheirButtons_AndTheButtonsAreInTheTabOrder()
    {
        string[] widgetRoles = ["option", "button", "link", "checkbox", "radio", "menuitem", "tab", "switch", "listbox", "treeitem", "gridcell"];
        var page = Checked(ExperienceLevel.Expert, SpellingFix(), GrammarFix());
        var list = page.Find(Id("lyric-assist-list"));

        var controls = list.QuerySelectorAll("button, select");
        controls.Length.ShouldBeGreaterThan(4);
        foreach (var control in controls)
        {
            control.GetAttribute("tabindex").ShouldNotBe("-1", control.OuterHtml);
            for (var parent = control.ParentElement; parent is not null && parent != list.ParentElement; parent = parent.ParentElement)
            {
                widgetRoles.ShouldNotContain(parent.GetAttribute("role") ?? string.Empty, parent.OuterHtml);
            }
        }
        list.QuerySelectorAll("[role=option], [aria-activedescendant]").Length.ShouldBe(0);
    }

    [Fact]
    public void AccessibleNames_ContainTheVisibleText()
    {
        var page = Checked(ExperienceLevel.Expert, SpellingFix(), GrammarFix());
        var list = page.Find(Id("lyric-assist-list"));

        list.QuerySelectorAll("[aria-label]").Where(e => e.TagName != "SELECT").ShouldBeEmpty();
        list.QuerySelectorAll("[aria-labelledby]").ShouldBeEmpty();
        page.Find(Id($"lyric-suggestion-accept-{Spelling}")).TextContent.Trim().ShouldBe("Accept");
        page.Find(Id($"lyric-suggestion-{Spelling}")).TextContent.ShouldContain("Line 2, Spelling: typo");
    }

    [Fact]
    public void TheFocusedEntry_IsMarkedCurrent_AndOnlyThatOne()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        page.FindAll("[aria-current]").ShouldBeEmpty();
        page.Find(".ss-lyrics-workspace").KeyDown(Key("F8"));

        page.FindAll("[aria-current=true]").Select(e => e.GetAttribute("data-testid")).ShouldBe([$"lyric-suggestion-{Spelling}"]);
        page.Find(Id($"lyric-suggestion-{Spelling}")).ClassList.ShouldContain("is-active");
        page.Find($"#{SuggestionEntry.EntryId(Spelling)}").GetAttribute("data-testid").ShouldBe($"lyric-suggestion-{Spelling}");
    }

    private static string? ActiveEntry(IRenderedComponent<CascadingValue<ExperienceContext>> page) =>
        page.Find("[data-testid=lyric-assist-list] > [aria-current=true]").GetAttribute("data-testid");

    [Theory]
    [InlineData("F8", "", SuggestionKey.Next)]
    [InlineData("F8", "shift", SuggestionKey.Previous)]
    [InlineData("Enter", "alt", SuggestionKey.Accept)]
    [InlineData("Enter", "", SuggestionKey.None)]
    [InlineData("Backspace", "alt", SuggestionKey.Reject)]
    [InlineData("i", "alt", SuggestionKey.Intentional)]
    [InlineData("Escape", "", SuggestionKey.Back)]
    [InlineData("c", "alt+ctrl", SuggestionKey.Check)]
    [InlineData("c", "ctrl", SuggestionKey.None)]
    public void Keys_MapAsTheContractSays(string key, string modifiers, SuggestionKey expected) =>
        SuggestionKeys.Of(Key(key, modifiers.Contains("alt", StringComparison.Ordinal), modifiers.Contains("ctrl", StringComparison.Ordinal), modifiers.Contains("shift", StringComparison.Ordinal)))
            .ShouldBe(expected);
}
