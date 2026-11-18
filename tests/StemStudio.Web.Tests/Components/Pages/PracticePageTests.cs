using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Practice;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Practice;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Components.Pages;

/// <summary>T098: the Practice page (FR-026..FR-028; ui-test-ids 1.13.0 practice-*).</summary>
public sealed class PracticePageTests : PageTestContext
{
    private static readonly PracticeSessionDto Session = PracticeFlowData.Session();

    public PracticePageTests()
    {
        Api.GetPracticeLogSettingsAsync(Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings());
        Api.GetPracticeOverviewAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([PracticeFlowData.TrackTotal()]);
        Api.ListPracticeSessionsAsync(null, Arg.Any<CancellationToken>()).Returns([Session]);
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderAt(ExperienceLevel level) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<PracticePage>());

    [Fact]
    public void Overview_ListsTheSongs_AndTheSwitch()
    {
        var page = RenderAt(ExperienceLevel.Advanced);

        var row = page.Find($"[data-testid=practice-overview-row-{TestData.TrackId}]");
        row.TextContent.ShouldContain("Fake Song");
        row.TextContent.ShouldContain("2 h 30 min");
        row.TextContent.ShouldContain("17 Nov 2026");
        row.QuerySelector("a")!.GetAttribute("href").ShouldBe($"tracks/{TestData.TrackId}/mixer");
        page.Find("[data-testid=practice-enabled]").GetAttribute("aria-checked").ShouldBe("true");
        page.FindAll("[data-testid=practice-sessions]").ShouldBeEmpty();
        page.Find("[data-testid=practice-page]").TextContent.ShouldContain("never sent anywhere");
    }

    [Fact]
    public async Task TheSwitch_TurnsTheLogOff()
    {
        Api.UpdatePracticeLogSettingsAsync(Arg.Any<PracticeLogSettingsUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.LogSettings(enabled: false));
        var page = RenderAt(ExperienceLevel.Beginner);

        await page.InvokeAsync(() => page.Find("[data-testid=practice-enabled]").Click());

        await Api.Received(1).UpdatePracticeLogSettingsAsync(Arg.Is<PracticeLogSettingsUpdateRequest>(r => r.Enabled == false), Arg.Any<CancellationToken>());
        page.Find("[data-testid=practice-enabled]").GetAttribute("aria-checked").ShouldBe("false");
    }

    [Fact]
    public async Task DeleteAll_AfterConfirmation_ReloadsTheEmptyPage()
    {
        var page = RenderAt(ExperienceLevel.Advanced);
        Api.GetPracticeOverviewAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).Returns([]);

        await page.InvokeAsync(() => page.Find("[data-testid=practice-delete-all]").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=practice-delete-all-confirm]").Click());

        await Api.Received(1).DeletePracticeSessionsAsync(null, Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => page.Find("[data-testid=practice-empty]").ShouldNotBeNull());
    }

    [Fact]
    public async Task Expert_SeesAndDeletesSessions()
    {
        var page = RenderAt(ExperienceLevel.Expert);

        page.Find($"[data-testid=practice-session-{Session.Id}]").TextContent.ShouldContain("Fake Song: 17 Nov 09:00, 11 min");
        await page.InvokeAsync(() => page.Find($"[data-testid=practice-session-delete-{Session.Id}]").Click());

        await Api.Received(1).DeletePracticeSessionAsync(Session.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void AnApiError_IsShown()
    {
        Api.GetPracticeOverviewAsync(Arg.Any<int>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException("The API is down."));

        RenderAt(ExperienceLevel.Advanced).Find("[data-testid=practice-error]").TextContent.ShouldContain("The API is down.");
    }

    [Fact]
    public void TheRail_HasThePracticeLink()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));

        shell.Find("[data-testid=nav-practice]").GetAttribute("href").ShouldBe("practice");
    }

    [Fact]
    public void Texts_DurationsAndSummaries()
    {
        PracticeLogText.Duration(-5).ShouldBe("0 min");
        PracticeLogText.Duration(3725).ShouldBe("1 h 2 min");
        PracticeLogText.DaysSummary([new PracticeDayDto { Date = new DateOnly(2026, 11, 1), Seconds = 0 }]).ShouldBe("No practice in the last 1 days.");
        PracticeLogText.TempoSummary([]).ShouldStartWith("No speed trainer");
        Should.Throw<ArgumentNullException>(() => PracticeLogText.DaysSummary(null!));
        Should.Throw<ArgumentNullException>(() => PracticeLogText.TempoSummary(null!));
    }
}
