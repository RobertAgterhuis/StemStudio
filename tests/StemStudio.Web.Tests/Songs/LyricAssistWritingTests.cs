using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Components.Lyrics.Assist;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T042 and T051: devices, syllables and rhythm, writing help, rhyme finder and refusals (FR-013 - FR-017, FR-022, FR-023).</summary>
public sealed class LyricAssistWritingTests : LyricEditorTestContext
{
    private static readonly RhythmTargetDto Target = new() { Bpm = 92, BeatsPerBar = 4, BarsPerLine = 2, SyllablesPerBeat = 1.5 };

    [Fact]
    public void Devices_ForASelection_SendLinesAndInstruction()
    {
        EditorInterop.GetSelectionAsync(Arg.Any<ElementReference>()).Returns((40, 60));
        var page = Open(ExperienceLevel.Beginner);

        page.Find(Id("lyric-assist-instruction")).Input("darker");
        page.Find(Id("lyric-assist-devices")).Click();

        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId,
            Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Devices && r.LineFrom == 3 && r.LineTo == 3 && r.Instruction == "darker" && r.Count == null),
            Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void DeviceSuggestion_ShowsTypeSyllablesAndRhymePartner()
    {
        var page = Checked(ExperienceLevel.Beginner, DeviceIdea());

        var entry = page.Find(Id($"lyric-suggestion-{Device}"));
        entry.TextContent.ShouldContain("Line 3, Device: end rhyme");
        page.Find(Id($"lyric-suggestion-syllables-{Device}")).TextContent.ShouldBe("7 -> 6 syllables");
        entry.TextContent.ShouldContain("Rhymes with \"gaat\" (line 2)");
    }

    [Fact]
    public void LicenceType_IsNamedAsPoeticLicence()
    {
        var elision = Suggestion(Guid.NewGuid(), SuggestionTypeDto.LicenceElision, new AnchorDto { Line = 2, Start = 0, Length = 2, Original = "Ik" }, "'k");

        var page = Checked(ExperienceLevel.Beginner, elision);

        page.Find(Id($"lyric-suggestion-{elision.SuggestionId}")).TextContent.ShouldContain("Poetic licence: elision");
    }

    [Fact]
    public void Alternatives_UseTheLineAtTheCursor_AndTheCount()
    {
        EditorInterop.GetSelectionAsync(Arg.Any<ElementReference>()).Returns((12, 12));
        var page = Open(ExperienceLevel.Advanced);

        page.Find(Id("lyric-assist-count")).Change("5");
        page.Find(Id("lyric-assist-alternatives")).Click();

        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId,
            Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Alternatives && r.LineFrom == 2 && r.LineTo == 2 && r.Count == 5), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void Continue_AsksAfterTheBlock_AndTitles_ForTheSheet()
    {
        EditorInterop.GetSelectionAsync(Arg.Any<ElementReference>()).Returns((10, 10));
        Api.GetLyricAssistAsync(AssistId, Arg.Any<CancellationToken>()).Returns(Assist(AssistStatusDto.Completed));
        var page = Open(ExperienceLevel.Advanced);

        page.Find(Id("lyric-assist-continue")).Click();
        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId,
            Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Continue && r.LineFrom == 2 && r.LineTo == 3), Arg.Any<CancellationToken>()));
        page.WaitForAssertion(() => page.Find(Id("lyric-assist-titles")).HasAttribute("disabled").ShouldBeFalse());
        page.Find(Id("lyric-assist-titles")).Click();

        page.WaitForAssertion(() => Api.Received(1).CreateLyricAssistAsync(LyricsId,
            Arg.Is<CreateLyricAssistRequest>(r => r.Kind == AssistKindDto.Titles && r.LineFrom == null), Arg.Any<CancellationToken>()));
    }

    [Fact]
    public void Continuation_And_Title_ShowWhatTheyChange()
    {
        var more = Suggestion(Guid.NewGuid(), SuggestionTypeDto.WriteContinuation, new AnchorDto { Line = 3, Start = 0, Length = 0, Original = string.Empty }, "En verder") with { Operation = SuggestionOperationDto.InsertAfter };
        var title = Suggestion(Guid.NewGuid(), SuggestionTypeDto.WriteTitle, new AnchorDto { Line = 0, Start = 0, Length = 0, Original = string.Empty }, "Zomernacht") with { Operation = SuggestionOperationDto.SetTitle };

        var page = Checked(ExperienceLevel.Beginner, more, title);

        page.Find(Id($"lyric-suggestion-original-{more.SuggestionId}")).TextContent.ShouldBe("(new lines)");
        page.Find(Id($"lyric-suggestion-original-{title.SuggestionId}")).TextContent.ShouldBe("Zomeravond");
        page.Find(Id($"lyric-suggestion-{title.SuggestionId}")).TextContent.ShouldContain("Title, Writing help: title");
        page.FindAll(Id($"lyric-highlight-{more.SuggestionId}")).ShouldBeEmpty();
    }

    [Fact]
    public async Task AcceptingATitle_SetsTheTitleField()
    {
        var title = Suggestion(Guid.NewGuid(), SuggestionTypeDto.WriteTitle, new AnchorDto { Line = 0, Start = 0, Length = 0, Original = string.Empty }, "Zomernacht") with { Operation = SuggestionOperationDto.SetTitle };
        Api.AcceptLyricSuggestionsAsync(LyricsId, Arg.Any<SuggestionDecisionRequest>(), Arg.Any<CancellationToken>()).Returns(Accepted(Text, applied: [title.SuggestionId]) with { Title = "Zomernacht" });
        var page = Checked(ExperienceLevel.Beginner, title);

        await page.Find(Id($"lyric-suggestion-accept-{title.SuggestionId}")).ClickAsync(new());

        page.Find(Id("lyrics-title-input")).GetAttribute("value").ShouldBe("Zomernacht");
        page.Find(Id("lyric-assist-undo")).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public async Task SyllableGutter_CountsLocally_AndMarksDeviations()
    {
        Api.CountSyllablesAsync(Arg.Any<SyllableRequest>(), Arg.Any<CancellationToken>()).Returns(new SyllableReportDto
        {
            Language = AssistLanguageDto.Nl,
            Lines =
            [
                new SyllableLineDto { Line = 1, Syllables = 0, IsMarker = true, Deviates = false },
                new SyllableLineDto { Line = 2, Syllables = 15, IsMarker = false, Target = 12, Deviates = true },
                new SyllableLineDto { Line = 3, Syllables = 11, IsMarker = false, Target = 12, Deviates = false },
            ],
            TargetDurationSeconds = 84,
            WordsPerSecond = 4.4,
        });
        var page = Open(ExperienceLevel.Advanced);
        page.FindAll("[data-testid^=lyric-syllables-]").ShouldBeEmpty();

        await page.Find(Id("lyric-syllable-toggle")).ClickAsync(new());

        page.FindAll(Id("lyric-syllables-1")).ShouldBeEmpty();
        var line2 = page.Find(Id("lyric-syllables-2"));
        (line2.TextContent, line2.GetAttribute("data-deviates"), line2.GetAttribute("data-target")).ShouldBe(("15", "true", "12"));
        page.Find(Id("lyric-syllables-3")).GetAttribute("data-deviates").ShouldBe("false");
        var density = page.Find(Id("lyric-density"));
        density.TextContent.ShouldBe("4.4 words per second at 1:24 - hard to sing, more than 4 words per second");
        density.ClassList.ShouldContain("ss-notice");
        page.Find(Id("lyric-syllable-toggle")).GetAttribute("aria-pressed").ShouldBe("true");
        await Api.Received(1).CountSyllablesAsync(Arg.Is<SyllableRequest>(r => r.Text == Text && r.Language == AssistLanguageDto.Nl), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyllableGutter_RecountsAfterATypingPause()
    {
        Api.CountSyllablesAsync(Arg.Any<SyllableRequest>(), Arg.Any<CancellationToken>()).Returns(new SyllableReportDto { Language = AssistLanguageDto.Nl, Lines = [] });
        var page = Open(ExperienceLevel.Advanced);
        await page.Find(Id("lyric-syllable-toggle")).ClickAsync(new());

        var typing = page.Find(Id("lyrics-text-input")).InputAsync(new() { Value = "nieuw" });
        await Api.Received(1).CountSyllablesAsync(Arg.Any<SyllableRequest>(), Arg.Any<CancellationToken>());
        Time.Advance(SyllableCounter.Pause);
        await typing;

        await Api.Received(1).CountSyllablesAsync(Arg.Is<SyllableRequest>(r => r.Text == "nieuw"), Arg.Any<CancellationToken>());
        Api.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ShouldNotContain(nameof(Api.CreateLyricAssistAsync));
    }

    [Fact]
    public async Task RhythmTarget_IsSavedAndCleared()
    {
        Api.UpdateSheetAssistSettingsAsync(LyricsId, Arg.Any<SheetAssistSettingsUpdate>(), Arg.Any<CancellationToken>())
            .Returns(SheetAssistInfoDto.Default with { RhythmTarget = Target }, SheetAssistInfoDto.Default);
        var page = Open(ExperienceLevel.Advanced);
        page.Find(Id("lyric-rhythm-clear")).HasAttribute("disabled").ShouldBeTrue();

        await page.Find(Id("lyric-rhythm-bpm")).ChangeAsync(new() { Value = "92" });
        await page.Find(Id("lyric-rhythm-target")).SubmitAsync();
        await page.Find(Id("lyric-rhythm-clear")).ClickAsync(new());

        await Api.Received(1).UpdateSheetAssistSettingsAsync(LyricsId, Arg.Is<SheetAssistSettingsUpdate>(u => u.RhythmTarget == Target), Arg.Any<CancellationToken>());
        await Api.Received(1).UpdateSheetAssistSettingsAsync(LyricsId, Arg.Is<SheetAssistSettingsUpdate>(u => u.RhythmTarget == null && u.RhythmTargetSpecified), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RhythmTarget_OutOfRange_IsNotSent()
    {
        var page = Open(ExperienceLevel.Advanced);

        await page.Find(Id("lyric-rhythm-bpm")).ChangeAsync(new() { Value = "300" });
        await page.Find(Id("lyric-rhythm-target")).SubmitAsync();

        page.Find(Id("lyric-rhythm-target")).ParentElement!.TextContent.ShouldContain("Use BPM 40-220");
        await Api.DidNotReceive().UpdateSheetAssistSettingsAsync(Arg.Any<Guid>(), Arg.Any<SheetAssistSettingsUpdate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RhymeFinder_LooksUpTheWordAtTheCursor_AndInsertsARhyme()
    {
        EditorInterop.GetSelectionAsync(Arg.Any<ElementReference>()).Returns((43, 43));
        Api.FindRhymesAsync("nacht", AssistLanguageDto.Nl, Arg.Any<CancellationToken>()).Returns(new RhymeResultDto
        {
            Word = "nacht",
            Language = AssistLanguageDto.Nl,
            Source = "local",
            Rhymes = [new RhymeDto { Word = "lacht", Kind = RhymeKindDto.Perfect, Syllables = 1 }, new RhymeDto { Word = "gedachte", Kind = RhymeKindDto.Near, Syllables = 3 }],
        });
        var page = Open(ExperienceLevel.Beginner);

        await page.Find(Id("lyric-rhymes-open")).ClickAsync(new());

        page.Find(Id("lyric-rhymes-input")).GetAttribute("value").ShouldBe("nacht");
        var lacht = page.Find(Id("lyric-rhymes-result-lacht"));
        (lacht.GetAttribute("data-kind"), lacht.GetAttribute("data-syllables")).ShouldBe(("perfect", "1"));
        page.Find(Id("lyric-rhymes-result-gedachte")).GetAttribute("data-kind").ShouldBe("near");
        await page.Find(Id("lyric-rhymes-insert-lacht")).ClickAsync(new());

        page.Find(Id("lyrics-text-input")).GetAttribute("value").ShouldBe(Text.Insert(43, "lacht"));
        await EditorInterop.Received(1).SelectAsync(Arg.Any<ElementReference>(), 48, 48);
        Api.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ShouldNotContain(nameof(Api.CreateLyricAssistAsync));
    }

    [Fact]
    public async Task RhymeFinder_NothingFound_SaysSo()
    {
        Api.FindRhymesAsync("xyz", AssistLanguageDto.Nl, Arg.Any<CancellationToken>()).Returns(new RhymeResultDto { Word = "xyz", Language = AssistLanguageDto.Nl, Source = "local", Rhymes = [] });
        var page = Open(ExperienceLevel.Beginner);
        await page.Find(Id("lyric-rhymes-open")).ClickAsync(new());

        await page.Find(Id("lyric-rhymes-input")).InputAsync(new() { Value = "xyz" });
        await page.Find("[data-testid=lyric-rhymes-panel] form").SubmitAsync();

        page.Find(Id("lyric-rhymes-panel")).TextContent.ShouldContain("No rhymes found.");
    }

    [Fact]
    public void Withheld_IsCounted_AndTheTextIsNowhere()
    {
        Stream((LyricAssistEventTypes.Note, new LyricAssistNoteEventDto { Code = AssistNoteCodeDto.VerbatimWithheld, Message = "1 suggestion withheld" }),
            (LyricAssistEventTypes.Completed, Completed(spelling: 0, verbatim: 1)));
        var page = Open(ExperienceLevel.Beginner);

        page.Find(Id("lyric-assist-alternatives")).Click();

        page.WaitForAssertion(() => page.Find(Id("lyric-assist-withheld")).TextContent.ShouldBe("1 suggestion withheld (too close to existing lyrics)"));
    }

    [Fact]
    public void ImitationInstruction_IsRefused_WithItsOwnId()
    {
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<LyricAssistDto>>(_ => throw LyricAssistData.Problem(LyricAssistProblemTypes.Imitation, status: System.Net.HttpStatusCode.BadRequest));
        var page = Open(ExperienceLevel.Beginner);

        page.Find(Id("lyric-assist-instruction")).Input("like a famous singer");
        page.Find(Id("lyric-assist-continue")).Click();

        page.WaitForAssertion(() => page.Find(Id("lyric-assist-imitation-refusal")).TextContent
            .ShouldContain("The assistant does not write like a named artist."));
        page.FindAll(Id("lyric-assist-refusal")).ShouldBeEmpty();
    }

    [Fact]
    public void OtherRefusals_UseTheGeneralId()
    {
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<LyricAssistDto>>(_ => throw LyricAssistData.Problem(LyricAssistProblemTypes.AssistActive));
        var page = Open(ExperienceLevel.Beginner);

        page.Find(Id("lyric-assist-check")).Click();

        page.WaitForAssertion(() => page.Find(Id("lyric-assist-refusal")).GetAttribute("data-code").ShouldBe("assist-active"));
    }
}
