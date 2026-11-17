using Bunit;
using NSubstitute;
using StemStudio.Contracts.LyricAssist;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using static StemStudio.Web.Tests.Fakes.LyricAssistData;

namespace StemStudio.Web.Tests.Songs;

/// <summary>WCAG 2.2 AA checks of the lyric assistant in bUnit (SC-008): names contain the visible text (2.5.3), target size
/// (2.5.8), focus not obscured (2.4.11), labelled form fields and no colour-only highlights (1.4.1).</summary>
public sealed class LyricAssistAccessibilityTests : LyricEditorTestContext
{
    private static readonly string Css = RepoPaths.ReadWeb("wwwroot/css/components/lyrics.css");

    [Fact]
    public void EveryButton_HasANameThatContainsItsVisibleText()
    {
        var page = Checked(ExperienceLevel.Expert, SpellingFix(), GrammarFix());

        foreach (var button in page.FindAll(".ss-assist button"))
        {
            var text = button.TextContent.Trim();
            var label = button.GetAttribute("aria-label");
            (text.Length > 0 || label is not null).ShouldBeTrue(button.OuterHtml);
            if (label is not null && text.Length > 0)
            {
                label.ShouldStartWith(text, Case.Insensitive, button.OuterHtml);
            }
        }
    }

    [Fact]
    public void FormFields_AreLabelled()
    {
        var page = Open(ExperienceLevel.Expert);

        foreach (var field in page.FindAll(".ss-assist input, .ss-assist select, .ss-lyrics-editor-main input, .ss-lyrics-editor-main select"))
        {
            (field.Closest("label") is not null || field.HasAttribute("aria-label")).ShouldBeTrue(field.OuterHtml);
        }

        page.Find("label[for=lyrics-text]").ShouldNotBeNull();
    }

    [Fact]
    public void Targets_AreAtLeastTheMinimumSize()
    {
        Css.ShouldContain(".ss-assist .rz-button,");
        Css.ShouldContain("min-height: var(--ss-target-min);");
        Css.ShouldContain("min-width: var(--ss-target-min);");
    }

    [Fact]
    public void FocusedEntry_IsNotHiddenUnderADock_AndHighlightsDoNotRelyOnColour()
    {
        Css.ShouldContain("scroll-padding-bottom: calc(var(--ss-dock-inset, 0px) + var(--ss-space-2));");
        foreach (var style in new[] { "wavy", "double", "dotted", "dashed", "solid" })
        {
            Css.ShouldContain($"text-decoration-style: {style}");
        }

        Css.ShouldContain(".ss-lyric-input:focus-visible");
        Css.ShouldContain(".ss-assist-list:focus-visible");
    }

    [Fact]
    public void Dialogs_AreModalAndLabelled()
    {
        Api.CreateLyricAssistAsync(LyricsId, Arg.Any<CreateLyricAssistRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task<LyricAssistDto>>(_ => throw NotOwn());
        var page = Open(ExperienceLevel.Beginner);
        page.Find(Id("lyric-assist-check")).Click();
        page.WaitForElement(Id("lyric-assist-own-work-open")).Click();

        var dialog = page.WaitForElement(Id("lyric-own-work-dialog"));
        page.Find($"#{dialog.GetAttribute("aria-labelledby")}").TextContent.ShouldBe("This is my own text");
        page.Find($"#{dialog.GetAttribute("aria-describedby")}").GetAttribute("data-testid").ShouldBe("lyric-own-work-statement");
    }
}
