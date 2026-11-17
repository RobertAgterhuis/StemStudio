using Bunit;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Help;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>Accessibility pass over the practice controls (012 T064, FR-017; design-spec.md 9).</summary>
public sealed class PracticeAccessibilityTests : PracticeTestContext
{
    [Fact]
    public void KeyboardOrder_FollowsTheVisualOrder_ThroughThePanel()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        var order = page.FindAll("[data-testid=practice-panel] button, [data-testid=practice-panel] input, [data-testid=practice-panel] select, [data-testid=practice-panel] [tabindex='0']")
            .Select(e => e.GetAttribute("data-testid"))
            .Where(id => id is not null && !id.StartsWith("help-hint", StringComparison.Ordinal))
            .ToList();

        order.ShouldBe(
        [
            "practice-panel-toggle", "practice-speed-down", "practice-speed", "practice-speed-up", "practice-speed-input",
            "practice-transpose-down", "practice-transpose", "practice-transpose-up", "practice-transpose-input",
            "practice-reset", "countin-toggle", "countin-bars", "countin-every-pass",
            "trainer-toggle", "trainer-start", "trainer-step", "trainer-target", "trainer-passes",
        ]);
        page.FindAll("[data-testid=practice-panel] [tabindex]").Select(e => e.GetAttribute("tabindex")).ShouldNotContain(t => t != null && t.Length > 0 && t[0] != '-' && t != "0");
    }

    [Fact]
    public void EveryPracticeControl_HasAnAccessibleName()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        foreach (var control in page.FindAll("[data-testid=practice-panel] button, [data-testid=practice-panel] input, [data-testid=practice-panel] select, [role=spinbutton]"))
        {
            var name = control.GetAttribute("aria-label") ?? control.GetAttribute("aria-labelledby") ?? control.TextContent.Trim();
            name.ShouldNotBeNullOrWhiteSpace(control.GetAttribute("data-testid"));
        }
    }

    [Fact]
    public void LiveRegions_AreOnlyTheTrainerStatus_AndTheCountInStartAndEnd()
    {
        var page = RenderAt();
        RaiseStatus(PracticeStatus.Initial with { CountIn = new CountInStatus(true, true, 4, 4) });
        page.WaitForAssertion(() => page.Find("[data-testid=countin-indicator]").ShouldNotBeNull());

        var live = page.FindAll("[data-testid=practice-panel] [aria-live], [data-testid=practice-panel] [role=status], [data-testid=practice-panel] [role=alert], .ss-countin-slot [aria-live]")
            .Select(e => e.GetAttribute("data-testid") ?? (e.HasAttribute("data-countin-announcer") ? "announcer" : e.TagName))
            .ToList();

        live.ShouldBe(["trainer-status", "announcer", "countin-indicator"]);
    }

    [Fact]
    public void SpokenValues_UsePercentAndSemitones()
    {
        var page = RenderAt();

        page.Find("[data-testid=practice-speed]").Input("75");
        page.Find("[data-testid=practice-transpose-up]").Click();
        page.Find("[data-testid=practice-transpose-up]").Click();

        page.Find("[data-testid=practice-speed]").GetAttribute("aria-valuetext").ShouldBe("75 percent");
        page.Find("[data-testid=practice-transpose]").GetAttribute("aria-valuetext").ShouldBe("+2 semitones");
    }

    [Fact]
    public void Switches_UseTheRadzenButtonFocusRing_AndThePrimaryPairForOn()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/practice.css");
        var basecss = RepoPaths.ReadWeb("wwwroot/css/base.css");

        basecss.ShouldContain(".rz-button:focus-visible");
        css.ShouldContain(".ss-switch[aria-checked=\"true\"]");
        css.ShouldContain("var(--ss-color-on-primary)");
        css.ShouldContain(".ss-practice-spin:focus-visible");
    }

    [Fact]
    public void TouchedHelpTopics_AreReviewedFor012()
    {
        var catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

        foreach (var id in new[] { "practice-speed", "practice-transpose", "practice-sound-quality", "practice-speed-trainer", "practice-count-in",
                     "semitone", "count-in", "mixer-screen", "mixer-metronome", "export-dialog", "shortcuts" })
        {
            catalog.Topic(id)!.Reviewed.ShouldBe("012", id);
        }
    }
}
