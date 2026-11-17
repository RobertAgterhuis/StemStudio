using Bunit;
using Microsoft.JSInterop;
using StemStudio.Contracts.Mixes;
using StemStudio.Web.Experience;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>
/// The practice panel is a disclosure (012 integration, CTO decision 2026-11-17; ui-test-ids 1.11.0 <c>practice-panel-toggle</c>,
/// <c>practice-panel-body</c>, <c>practice-panel-summary</c>): the browser's last choice wins; without one the panel is
/// open only while the practice settings are not neutral.
/// </summary>
public sealed class PracticePanelDisclosureTests : PracticeTestContext
{
    private const string Key = "stemstudio.practice-panel";

    [Fact]
    public void NeutralSettings_WithoutAStoredChoice_StartCollapsed_AsADisclosureOverTheBody()
    {
        var page = RenderAt(ExperienceLevel.Beginner);

        var toggle = page.Find("[data-testid=practice-panel-toggle]");
        toggle.TagName.ShouldBe("BUTTON");
        toggle.GetAttribute("aria-expanded").ShouldBe("false");
        toggle.TextContent.ShouldContain("Practice tempo");
        var body = page.Find($"#{toggle.GetAttribute("aria-controls")}");
        body.GetAttribute("data-testid").ShouldBe("practice-panel-body");
        body.HasAttribute("hidden").ShouldBeTrue();
        page.Find("[data-testid=practice-panel]").GetAttribute("data-expanded").ShouldBe("false");
        page.FindAll("[data-testid=practice-panel-summary]").ShouldBeEmpty();
    }

    [Fact]
    public void Toggle_OpensAndClosesTheBody_AndRemembersTheChoiceInThisBrowser()
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-panel-toggle]").Click();

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[data-testid=practice-panel-body]").HasAttribute("hidden").ShouldBeFalse();
        JSInterop.VerifyInvoke("localStorage.setItem").Arguments.ShouldBe([Key, "{\"expanded\":true}"]);

        page.Find("[data-testid=practice-panel-toggle]").Click();

        page.Find("[data-testid=practice-panel-body]").HasAttribute("hidden").ShouldBeTrue();
        JSInterop.VerifyInvoke("localStorage.setItem", calledTimes: 2)[1].Arguments.ShouldBe([Key, "{\"expanded\":false}"]);
    }

    [Fact]
    public void NonNeutralSettings_WithoutAStoredChoice_StartExpanded()
    {
        MixWith(At(75, 2));

        var page = RenderAt(withMix: true);

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
        page.Find("[data-testid=practice-panel-body]").HasAttribute("hidden").ShouldBeFalse();
    }

    [Fact]
    public void ACountInAlone_IsNotNeutral_ForTheDisclosure()
    {
        MixWith(PracticeSettingsDto.Default with { CountIn = new CountInSettingsDto(1, false) });

        var page = RenderAt(withMix: true);

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public void AStoredCollapse_Wins_AndTheHeaderSummarisesWhatIsNotNeutral()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("{\"expanded\":false}");
        MixWith(At(75, 2) with { CountIn = new CountInSettingsDto(1, false) });

        var page = RenderAt(withMix: true);

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("false");
        page.Find("[data-testid=practice-panel-summary]").TextContent.Trim().ShouldBe("75 %, +2 st, count-in");
    }

    [Fact]
    public void TheSummary_FollowsTheSpeedHeardNow_WhileTheTrainerRuns()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("{\"expanded\":false}");
        var page = RenderAt();

        RaiseStatus(Running(85));

        page.WaitForAssertion(() => page.Find("[data-testid=practice-panel-summary]").TextContent.Trim().ShouldBe("85 %"));
    }

    [Fact]
    public void AStoredExpand_Wins_AtNeutralSettings()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult("{\"expanded\":true}");

        var page = RenderAt();

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"expanded\":\"yes\"}")]
    [InlineData("{}")]
    [InlineData("null")]
    public void AnUnusableStoredValue_FallsBackToTheSettings(string stored)
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetResult(stored);

        var page = RenderAt();

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public void BlockedStorage_StillOpensThePanel_ForThisVisit()
    {
        JSInterop.Setup<string?>("localStorage.getItem", Key).SetException(new JSException("SecurityError: storage is disabled"));
        JSInterop.SetupVoid("localStorage.setItem", _ => true).SetException(new JSException("QuotaExceededError"));
        var page = RenderAt();

        page.Find("[data-testid=practice-panel-toggle]").Click();

        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("aria-expanded").ShouldBe("true");
    }

    [Fact]
    public void TheHeadingNamesTheGroup_AndTheHelpHintStaysOutsideTheButton()
    {
        var page = RenderAt();

        var panel = page.Find("[data-testid=practice-panel]");
        var title = page.Find($"#{panel.GetAttribute("aria-labelledby")}");
        title.TagName.ShouldBe("H2");
        title.QuerySelector("[data-testid=practice-panel-toggle]").ShouldNotBeNull();
        page.Find("[data-testid=practice-panel-toggle]").QuerySelector("button").ShouldBeNull();
        page.Find("[data-testid=practice-panel-toggle]").GetAttribute("data-help-id").ShouldBe("practice-panel");
    }
}
