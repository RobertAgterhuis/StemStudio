using Bunit;
using NSubstitute;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Experience;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>023 T022: the pane shell in the lyrics editor (FR-001, FR-027, FR-029, FR-030; ui-test-ids 1.23.0).</summary>
public sealed class LyricAssistPaneTests : LyricEditorTestContext
{
    [Fact]
    public void Page_ShowsThe010Fields_AndThePaneIsClosedUntilToggled()
    {
        var page = RenderAtLevel<LyricsEditorPage>(ExperienceLevel.Beginner, p => p.Add(x => x.LyricsId, LyricsId));

        page.WaitForElement(Id("lyrics-text-input")).GetAttribute("value").ShouldBe(Text);
        page.Find(Id("lyrics-title-input")).GetAttribute("value").ShouldBe("Zomeravond");
        page.Find(Id("lyrics-language-select")).ShouldNotBeNull();
        page.FindAll(Id("lyric-assist-pane")).ShouldBeEmpty();
        var toggle = page.Find(Id("lyric-assist-toggle"));
        toggle.GetAttribute("aria-expanded").ShouldBe("false");

        toggle.Click();

        var pane = page.Find(Id("lyric-assist-pane"));
        (pane.GetAttribute("role"), pane.GetAttribute("aria-label"), pane.GetAttribute("data-state")).ShouldBe(("region", "Lyric assistant", "idle"));
        page.Find(Id("lyric-assist-toggle")).GetAttribute("aria-expanded").ShouldBe("true");
        Api.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ShouldNotContain(nameof(Api.CreateLyricAssistAsync));
    }

    [Fact]
    public void Toggle_HidesThePaneAgain_WithoutStartingAnything()
    {
        var page = Open();

        page.Find(Id("lyric-assist-toggle")).Click();

        page.FindAll(Id("lyric-assist-pane")).ShouldBeEmpty();
        Api.ReceivedCalls().Select(c => c.GetMethodInfo().Name).ShouldNotContain(nameof(Api.CreateLyricAssistAsync));
    }

    [Fact]
    public void LayersLine_NamesDictionaryAndModel_FromAdvanced()
    {
        var page = Open(ExperienceLevel.Advanced);

        var layers = page.Find(Id("lyric-assist-layers"));
        layers.TextContent.ShouldBe("Spelling: OpenTaal dictionary - grammar and flow: qwen3.5:9b, local");
        (layers.GetAttribute("data-backend"), layers.GetAttribute("data-model"), layers.GetAttribute("data-fallback")).ShouldBe(("ollama", "qwen3.5:9b", "false"));
        page.FindAll(Id("lyric-assist-unavailable")).ShouldBeEmpty();
    }

    [Fact]
    public void Beginner_SeesTheCoreActions_ButNotTheAdvancedOnes()
    {
        var page = Open(ExperienceLevel.Beginner);

        foreach (var id in new[] { "lyric-assist-check", "lyric-assist-devices", "lyric-assist-alternatives", "lyric-assist-continue", "lyric-assist-instruction", "lyric-rhymes-open", "lyric-assist-accept-all", "lyric-assist-undo", "lyric-assist-live" })
        {
            page.FindAll(Id(id)).Count.ShouldBe(1, id);
        }

        foreach (var id in new[] { "lyric-assist-layers", "lyric-assist-titles", "lyric-assist-count", "lyric-assist-sensitivity", "lyric-assist-filter-all", "lyric-syllable-toggle", "lyric-rhythm-target", "lyric-marks-open", "lyric-words-open", "lyric-assist-log", "lyric-revisions-open" })
        {
            page.FindAll(Id(id)).ShouldBeEmpty(id);
        }
    }

    [Fact]
    public void Advanced_AddsTheAdvancedControls_ExpertAddsLogAndRevisions()
    {
        var advanced = Open(ExperienceLevel.Advanced);
        foreach (var id in new[] { "lyric-assist-titles", "lyric-assist-count", "lyric-assist-sensitivity", "lyric-assist-filter-all", "lyric-syllable-toggle", "lyric-rhythm-target", "lyric-marks-open", "lyric-words-open" })
        {
            advanced.FindAll(Id(id)).Count.ShouldBe(1, id);
        }

        advanced.FindAll(Id("lyric-assist-log")).ShouldBeEmpty();
        var expert = Open(ExperienceLevel.Expert);
        expert.FindAll(Id("lyric-assist-log")).Count.ShouldBe(1);
        expert.FindAll(Id("lyric-revisions-open")).Count.ShouldBe(1);
    }

    [Fact]
    public void ModelOff_ShowsTheReasonAndFix_AndDisablesWritingHelp()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(AgentBackendDto.None, ready: false));

        var page = Open();

        page.Find(Id("lyric-assist-unavailable")).TextContent
            .ShouldBe("Grammar, flow and writing help need the AI model: The AI model is switched off in Settings. Choose a model backend in Settings > AI assistant.");
        foreach (var id in new[] { "lyric-assist-devices", "lyric-assist-alternatives", "lyric-assist-continue", "lyric-assist-titles" })
        {
            var button = page.Find(Id(id));
            button.HasAttribute("disabled").ShouldBeTrue(id);
            button.GetAttribute("title").ShouldStartWith("Needs the AI model:");
        }

        page.Find(Id("lyric-assist-check")).HasAttribute("disabled").ShouldBeFalse();
        page.Find(Id("lyric-assist-layers")).TextContent.ShouldEndWith("grammar and flow: off");
    }

    [Fact]
    public void ModelNotReachable_WithoutReason_SaysSo()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status() with
        {
            Model = Status().Model with { Ready = false, Reason = null, Fix = null },
        });

        var page = Open();

        page.Find(Id("lyric-assist-unavailable")).TextContent.ShouldBe("Grammar, flow and writing help need the AI model: it is not reachable.");
    }

    [Fact]
    public void DictionaryMissing_IsReported()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status() with
        {
            Dictionary = Layer(SuggestionSourceDto.Dictionary, "OpenTaal dictionary", available: false) with { Reason = "The dictionary files are missing.", Fix = "Reinstall." },
        });

        var page = Open();

        page.Find(Id("lyric-assist-unavailable")).TextContent.ShouldBe("Spelling is unavailable: The dictionary files are missing. Reinstall.");
        page.Find(Id("lyric-assist-pane")).GetAttribute("data-state").ShouldBe("unavailable");
    }

    [Fact]
    public void QualityNote_IsShown()
    {
        Api.GetLyricAssistStatusAsync(Arg.Any<CancellationToken>()).Returns(Status(qualityNote: "Dutch grammar and flow: limited quality with the local model"));

        var page = Open();

        page.Find(Id("lyric-assist-quality-note")).TextContent.ShouldContain("Dutch grammar and flow: limited quality with the local model");
    }

    [Theory]
    [InlineData("de", "The assistant works on Dutch and English lyrics only.")]
    [InlineData("unknown", "Choose the language of the lyrics first: the assistant works on Dutch and English lyrics.")]
    public void UnsupportedLanguage_DisablesTheAssistant_WithTheReason(string language, string reason)
    {
        var page = Open(sheet: Sheet(language));

        page.Find(Id("lyric-assist-unavailable")).TextContent.ShouldBe(reason);
        page.Find(Id("lyric-assist-pane")).GetAttribute("data-state").ShouldBe("unavailable");
        page.Find(Id("lyric-assist-check")).HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task UnknownLanguage_OffersTheGuess_AndSetsIt()
    {
        Api.GuessLyricsLanguageAsync(Text, Arg.Any<CancellationToken>()).Returns(new LanguageGuessDto { Language = AssistLanguageDto.Nl, Confidence = 0.92 });
        Api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet("nl", revision: 4));

        var page = Open(sheet: Sheet("unknown"));

        page.Find(Id("lyric-assist-language-guess")).TextContent.ShouldContain("Looks like Dutch");
        await page.Find(Id("lyric-assist-language-apply")).ClickAsync(new());

        await Api.Received(1).UpdateLyricsSheetAsync(LyricsId, Arg.Is<LyricsInput>(i => i.Language == "nl"), Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => page.FindAll(Id("lyric-assist-language-guess")).ShouldBeEmpty());
        page.Find(Id("lyric-assist-check")).HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Pane_LinksToItsHelpTopics()
    {
        var page = Open(ExperienceLevel.Expert);

        foreach (var topic in new[] { "lyric-assist", "lyric-assist-devices", "lyric-assist-licence", "lyric-assist-rhythm", "lyric-assist-privacy" })
        {
            page.FindAll($"[data-help-id={topic}]").ShouldNotBeEmpty(topic);
        }
    }

    [Fact]
    public void MissingSheet_ShowsTheLoadError()
    {
        Api.GetLyricsSheetAsync(LyricsId, Arg.Any<CancellationToken>()).Returns<Task<LyricsSheetDto>>(_ => throw Problem(System.Net.HttpStatusCode.NotFound, "Not found"));

        var page = RenderAtLevel<LyricsEditorPage>(ExperienceLevel.Beginner, p => p.Add(x => x.LyricsId, LyricsId));

        page.WaitForElement(Id("lyrics-load-error"));
        page.FindAll(Id("lyric-assist-toggle")).ShouldBeEmpty();
    }

    [Fact]
    public async Task Save_SendsTheExpectedRevision_AndValidatesTheTitle()
    {
        Api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(Sheet(text: "nieuw", revision: 4));
        var page = Open();

        await page.Find(Id("lyrics-title-input")).InputAsync(new() { Value = " " });
        await page.Find(Id("lyrics-save")).ClickAsync(new());
        page.Find(Id("lyrics-error")).GetAttribute("data-field").ShouldBe("title");

        await page.Find(Id("lyrics-title-input")).InputAsync(new() { Value = "Zomeravond" });
        await page.Find(Id("lyrics-text-input")).InputAsync(new() { Value = "nieuw" });
        page.Markup.ShouldContain("Unsaved changes");
        await page.Find(Id("lyrics-save")).ClickAsync(new());

        await Api.Received(1).UpdateLyricsSheetAsync(LyricsId, Arg.Is<LyricsInput>(i => i.ExpectedRevision == 3 && i.Text == "nieuw"), Arg.Any<CancellationToken>());
        page.FindAll(Id("lyrics-error")).ShouldBeEmpty();
        page.Markup.ShouldNotContain("Unsaved changes");
    }

    [Fact]
    public async Task Save_Problem_IsShown()
    {
        Api.UpdateLyricsSheetAsync(LyricsId, Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>())
            .Returns<Task<LyricsSheetDto>>(_ => throw Problem(System.Net.HttpStatusCode.Conflict, "Changed elsewhere"));
        var page = Open();

        await page.Find(Id("lyrics-save")).ClickAsync(new());

        page.Find(Id("lyrics-error")).TextContent.ShouldContain("Changed elsewhere");
    }
}
