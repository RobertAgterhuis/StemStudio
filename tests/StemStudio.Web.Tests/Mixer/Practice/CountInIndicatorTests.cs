using Bunit;
using StemStudio.Web.Audio;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Mixer.Practice;

/// <summary>The visual count (012 T059, FR-013, FR-017; ui-test-ids 1.11.0 countin-indicator, countin-dot-n).</summary>
public sealed class CountInIndicatorTests : PracticeTestContext
{
    private static PracticeStatus Counting(int remaining, int total = 4) =>
        PracticeStatus.Initial with { CountIn = new CountInStatus(true, true, remaining, total) };

    private static readonly PracticeStatus Done = PracticeStatus.Initial with { CountIn = new CountInStatus(true, false, 0, 4) };

    [Fact]
    public void Indicator_ExistsOnlyWhileACountInRuns()
    {
        var page = RenderAt(ExperienceLevel.Beginner);
        page.FindAll("[data-testid=countin-indicator]").ShouldBeEmpty();

        RaiseStatus(Counting(4));
        page.WaitForAssertion(() => page.Find("[data-testid=countin-indicator]").ShouldNotBeNull());
        RaiseStatus(Done);

        page.WaitForAssertion(() => page.FindAll("[data-testid=countin-indicator]").ShouldBeEmpty());
    }

    [Fact]
    public void Indicator_IsAPoliteStatus_NearTheTransport()
    {
        var page = RenderAt();

        RaiseStatus(Counting(3));

        var indicator = page.WaitForElement("[data-testid=countin-indicator]");
        (indicator.GetAttribute("role"), indicator.GetAttribute("aria-live")).ShouldBe(("status", "polite"));
        (indicator.GetAttribute("data-beats-remaining"), indicator.GetAttribute("data-beats-total")).ShouldBe(("3", "4"));
        indicator.Closest(".ss-mixer-dock").ShouldNotBeNull();
    }

    [Theory]
    [InlineData(4, "current", "todo", "todo", "todo")]
    [InlineData(2, "done", "done", "current", "todo")]
    [InlineData(1, "done", "done", "done", "current")]
    public void Dots_ShowDoneCurrentAndTodo(int remaining, string first, string second, string third, string fourth)
    {
        var page = RenderAt();

        RaiseStatus(Counting(remaining));

        page.WaitForAssertion(() => page.Find("[data-testid=countin-dot-1]").GetAttribute("data-state").ShouldBe(first));
        new[] { "countin-dot-2", "countin-dot-3", "countin-dot-4" }.Select(id => page.Find($"[data-testid={id}]").GetAttribute("data-state"))
            .ShouldBe([second, third, fourth]);
        page.FindAll("[data-testid=countin-dot-5]").ShouldBeEmpty();
    }

    [Fact]
    public void TwoBarsInThreeFour_HaveSixDots()
    {
        var page = RenderAt();

        RaiseStatus(Counting(6, total: 6));

        page.WaitForAssertion(() => page.FindAll("[data-testid^=countin-dot-]").Count.ShouldBe(6));
    }

    [Fact]
    public void Beats_AreNotAnnounced_OnlyCountInAndGo()
    {
        var page = RenderAt();

        RaiseStatus(Counting(4));
        page.WaitForAssertion(() => page.Find("[data-testid=countin-indicator]").ShouldNotBeNull());
        var indicator = page.Find("[data-testid=countin-indicator]");
        indicator.Children.ShouldAllBe(c => c.GetAttribute("aria-hidden") == "true");
        Announcer(page).ShouldBe("Count-in");
        RaiseStatus(Counting(3));
        page.WaitForAssertion(() => page.Find("[data-testid=countin-indicator]").GetAttribute("data-beats-remaining").ShouldBe("3"));
        Announcer(page).ShouldBe("Count-in");

        RaiseStatus(Done);

        page.WaitForAssertion(() => Announcer(page).ShouldBe("Go"));
    }

    [Fact]
    public void Announcer_IsSilent_BeforeAnyCountIn()
    {
        var page = RenderAt();

        Announcer(page).ShouldBeEmpty();
        page.Find(".ss-mixer-dock [data-countin-announcer]").GetAttribute("role").ShouldBe("status");
    }

    [Fact]
    public void Number_ShowsTheBeatsLeft_ForSightedUsers()
    {
        var page = RenderAt();

        RaiseStatus(Counting(2));

        page.WaitForAssertion(() => page.Find("[data-testid=countin-indicator] .ss-countin-number").TextContent.Trim().ShouldBe("2"));
    }

    [Fact]
    public void ReducedMotion_StopsThePulse_AndTheCurrentBeatIsARingNotAFlash()
    {
        var css = RepoPaths.ReadWeb("wwwroot/css/components/practice.css");

        css.ShouldContain("html[data-motion=\"reduced\"] .ss-countin-dot[data-state=\"current\"]");
        css.ShouldContain("prefers-reduced-motion: reduce");
        css.ShouldNotContain("animation");
    }

    private static string Announcer(Bunit.IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> page) =>
        page.Find("[data-countin-announcer]").TextContent.Trim();
}
