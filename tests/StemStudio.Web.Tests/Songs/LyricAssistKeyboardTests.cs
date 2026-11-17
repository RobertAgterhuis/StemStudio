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

        var list = page.Find(Id("lyric-assist-list"));
        list.GetAttribute("aria-activedescendant").ShouldBe(SuggestionEntry.OptionId(Spelling));
        page.Find($"#{SuggestionEntry.OptionId(Spelling)}").GetAttribute("aria-selected").ShouldBe("true");
        page.Find(Id("lyric-assist-live")).TextContent.ShouldBe("Line 2, spelling error: wiet, proposed weet. Uitleg bij de suggestie.");
        EditorInterop.Received(1).SelectAsync(Arg.Any<ElementReference>(), 11, 15);
        EditorInterop.Received(1).FocusAsync(SuggestionList.ListId);
        page.Find($"[data-testid=lyric-highlight-{Spelling}]").ClassList.ShouldContain("is-active");
    }

    [Fact]
    public void ShiftF8_GoesBack_AndWraps()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());

        page.Find(".ss-lyrics-workspace").KeyDown(Key("F8", shift: true));

        page.Find(Id("lyric-assist-list")).GetAttribute("aria-activedescendant").ShouldBe(SuggestionEntry.OptionId(Grammar));
    }

    [Fact]
    public void F8_InTheList_MovesWithinTheList_Once()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), GrammarFix());
        var list = page.Find(Id("lyric-assist-list"));

        list.KeyDown(Key("F8"));
        page.Find(Id("lyric-assist-list")).KeyDown(Key("F8"));

        page.Find(Id("lyric-assist-list")).GetAttribute("aria-activedescendant").ShouldBe(SuggestionEntry.OptionId(Grammar));
    }

    [Fact]
    public void FocusingTheList_SelectsTheFirstEntry_WithoutMovingTheFocusAway()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix());

        page.Find(Id("lyric-assist-list")).Focus();

        page.Find(Id("lyric-assist-list")).GetAttribute("aria-activedescendant").ShouldBe(SuggestionEntry.OptionId(Spelling));
        EditorInterop.DidNotReceive().FocusAsync(Arg.Any<string>());
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
    public void Options_AreLabelledByTheirVisibleTexts_AndButtonsAreNamedByTheirText()
    {
        var page = Checked(ExperienceLevel.Beginner, SpellingFix(), DeviceIdea() with { Proposal = null });

        var option = page.Find($"#{SuggestionEntry.OptionId(Spelling)}");
        var labels = option.GetAttribute("aria-labelledby")!.Split(' ');
        labels.Select(id => page.Find($"#{id}").TextContent).ShouldBe(["Line 2, Spelling: typo", "wiet", "weet"]);
        page.Find($"#{SuggestionEntry.OptionId(Device)}").GetAttribute("aria-labelledby")!.Split(' ').Length.ShouldBe(2);
        page.Find(Id($"lyric-suggestion-accept-{Spelling}")).TextContent.Trim().ShouldBe("Accept");
        page.Find(Id($"lyric-suggestion-accept-{Spelling}")).GetAttribute("tabindex").ShouldBe("-1");
        page.Find(Id("lyric-assist-live")).GetAttribute("aria-live").ShouldBe("polite");
    }

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
