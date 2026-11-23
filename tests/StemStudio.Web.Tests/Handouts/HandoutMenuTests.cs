using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Handouts;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>
/// "Lesson handout" menu (T076, US3 scenarios 1-3, FR-008, FR-010; ui-test-ids 1.19.0): seven families, one click creates
/// the handout with the defaults, the saved status with "Open in library", the "Not included" list, the WAI-ARIA menu
/// button keyboard pattern and the Advanced "Customise..." entry.
/// </summary>
public sealed class HandoutMenuTests : PageTestContext
{
    private static readonly HandoutSourceDto Song = HandoutSourceDto.Song(TestData.TrackId);

    public HandoutMenuTests()
    {
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>()).Returns(InsightsData.Handout());
        Api.GetHandoutOptionsAsync(Arg.Any<HandoutSourceDto>(), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns(InsightsData.BassOptions());
    }

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderMenu(ExperienceLevel level = ExperienceLevel.Beginner, string prefix = "handout") =>
        RenderAtLevel<HandoutMenu>(level, p => p.Add(c => c.Source, Song).Add(c => c.Prefix, prefix));

    [Fact]
    public void Button_IsAMenuButton_ClosedAtFirst()
    {
        var page = RenderMenu();

        var button = page.Find("[data-testid=handout-menu]");
        button.LocalName.ShouldBe("button");
        button.TextContent.ShouldContain("Lesson handout");
        (button.GetAttribute("aria-haspopup"), button.GetAttribute("aria-expanded")).ShouldBe(("menu", "false"));
        page.FindAll("[role=menu]").ShouldBeEmpty();
    }

    [Fact]
    public void Open_ListsTheSevenFamilies_InOrder_ButNoCustomiseAtBeginner()
    {
        var page = RenderMenu();

        page.Find("[data-testid=handout-menu]").Click();

        var menu = page.Find("[role=menu]");
        menu.GetAttribute("aria-label").ShouldBe("Lesson handout");
        page.Find("[data-testid=handout-menu]").GetAttribute("aria-controls").ShouldBe(menu.Id);
        page.FindAll("[role=menuitem]").Select(i => i.GetAttribute("data-testid")).ShouldBe(
            ["handout-family-overview", "handout-family-vocals", "handout-family-guitar", "handout-family-bass", "handout-family-keys", "handout-family-drums", "handout-family-melodic"]);
        page.FindAll("[role=menuitem]").Select(i => i.TextContent.Trim()).ShouldBe(
            ["Band overview", "Vocals", "Guitar", "Bass", "Keys", "Drums", "Melodic instruments"]);
        page.FindAll("[data-testid=handout-customise]").ShouldBeEmpty();
        page.FindAll("[role=menu] [role=none]").Count.ShouldBe(7);
    }

    [Fact]
    public async Task OneClick_CreatesTheHandoutWithTheDefaults_AndShowsTheSavedStatusWithItsLink()
    {
        var page = RenderMenu();
        page.Find("[data-testid=handout-menu]").Click();

        await page.Find("[data-testid=handout-family-bass]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(
            Arg.Is<CreateHandoutRequest>(r => r.Source.TrackId == TestData.TrackId && r.Family == HandoutFamilyDto.Bass && r.Include == null && r.NotationAssetIds == null && r.Paper == null),
            Arg.Any<CancellationToken>());
        page.FindAll("[role=menu]").ShouldBeEmpty();
        var saved = page.Find("[data-testid=handout-saved]");
        saved.GetAttribute("role").ShouldBe("status");
        saved.TextContent.ShouldContain("Bass handout");
        page.Find("[data-testid=handout-saved-open]").GetAttribute("href").ShouldBe($"library?asset={InsightsData.HandoutId}");
    }

    [Fact]
    public async Task OneClick_ListsTheMissingDefaultIngredients()
    {
        var page = RenderMenu();
        page.Find("[data-testid=handout-menu]").Click();

        await page.Find("[data-testid=handout-family-bass]").ClickAsync(new());

        var missing = page.Find("[data-testid=handout-missing]");
        missing.TextContent.ShouldContain("Not included");
        missing.TextContent.ShouldContain("Practice notes: No notes yet");
        missing.TextContent.ShouldContain("Sheet music: Bass score; tablature not generated yet");
        missing.TextContent.ShouldNotContain("Form chart");
    }

    [Fact]
    public async Task CreateError_IsShown_AndNothingIsSaved()
    {
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>())
            .Returns<AssetDto>(_ => throw Problem(HttpStatusCode.ServiceUnavailable, "Worker unavailable", "The processing worker is not reachable."));
        var page = RenderMenu();
        page.Find("[data-testid=handout-menu]").Click();

        await page.Find("[data-testid=handout-family-drums]").ClickAsync(new());

        page.Find("[data-testid=handout-error]").TextContent.ShouldContain("not reachable");
        page.FindAll("[data-testid=handout-saved]").ShouldBeEmpty();
    }

    [Fact]
    public async Task OptionsError_StillShowsTheSavedHandout_WithoutAMissingList()
    {
        Api.GetHandoutOptionsAsync(Arg.Any<HandoutSourceDto>(), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns<HandoutOptionsDto>(_ => throw Problem(HttpStatusCode.NotFound, "Not found"));
        var page = RenderMenu();
        page.Find("[data-testid=handout-menu]").Click();

        await page.Find("[data-testid=handout-family-keys]").ClickAsync(new());

        page.Find("[data-testid=handout-saved]").ShouldNotBeNull();
        page.FindAll("[data-testid=handout-missing]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Keyboard_FollowsTheMenuButtonPattern()
    {
        var page = RenderMenu();
        var button = page.Find("[data-testid=handout-menu]");

        await button.KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        page.Find("[data-testid=handout-family-overview]").GetAttribute("tabindex").ShouldBe("0");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        page.Find("[data-testid=handout-family-melodic]").GetAttribute("tabindex").ShouldBe("0");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowDown" });
        page.Find("[data-testid=handout-family-overview]").GetAttribute("tabindex").ShouldBe("0");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Home" });
        page.Find("[data-testid=handout-family-overview]").GetAttribute("tabindex").ShouldBe("0");
        page.FindAll("[role=menuitem][tabindex='0']").Count.ShouldBe(1);
    }

    [Fact]
    public async Task Escape_ClosesTheMenu()
    {
        var page = RenderMenu();
        page.Find("[data-testid=handout-menu]").Click();

        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=menu]").ShouldBeEmpty();
        page.Find("[data-testid=handout-menu]").GetAttribute("aria-expanded").ShouldBe("false");
    }

    [Fact]
    public async Task Keyboard_ArrowUpOnTheButton_OpensAtTheLastItem_TabCloses_OtherKeysDoNothing()
    {
        var page = RenderMenu(ExperienceLevel.Advanced);

        await page.Find("[data-testid=handout-menu]").KeyDownAsync(new KeyboardEventArgs { Key = "ArrowUp" });
        page.Find("[data-testid=handout-customise]").GetAttribute("tabindex").ShouldBe("0");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        page.Find("[data-testid=handout-customise]").GetAttribute("tabindex").ShouldBe("0");
        await page.Find("[role=menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });
        page.FindAll("[role=menu]").ShouldBeEmpty();
        await page.Find("[data-testid=handout-menu]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });
        page.FindAll("[role=menu]").ShouldBeEmpty();
    }

    [Fact]
    public void Toggle_ClosesAnOpenMenu()
    {
        var page = RenderMenu();

        page.Find("[data-testid=handout-menu]").Click();
        page.Find("[data-testid=handout-menu]").Click();

        page.FindAll("[role=menu]").ShouldBeEmpty();
    }

    [Fact]
    public void Advanced_AddsCustomise_WhichOpensTheDialog()
    {
        var page = RenderMenu(ExperienceLevel.Advanced);
        page.Find("[data-testid=handout-menu]").Click();

        var customise = page.Find("[data-testid=handout-customise]");
        customise.TextContent.Trim().ShouldBe("Customise...");
        customise.Click();

        page.Find("[data-testid=handout-dialog]").ShouldNotBeNull();
        page.FindAll("[role=menu]").ShouldBeEmpty();
    }

    [Fact]
    public void SetlistPrefix_NamesTheSetlistIds()
    {
        var page = RenderMenu(ExperienceLevel.Expert, "setlist-handout");

        page.Find("[data-testid=setlist-handout-menu]").TextContent.ShouldContain("Setlist handout");
        page.Find("[data-testid=setlist-handout-menu]").Click();

        page.Find("[data-testid=setlist-handout-family-overview]").ShouldNotBeNull();
        page.Find("[data-testid=setlist-handout-customise]").ShouldNotBeNull();
        page.Find("[role=menu]").GetAttribute("aria-label").ShouldBe("Setlist handout");
    }

    [Fact]
    public void Accessibility_EveryControlIsNamedWithItsVisibleText()
    {
        var page = RenderMenu(ExperienceLevel.Expert);
        page.Find("[data-testid=handout-menu]").Click();

        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find(".ss-handout-menu"));
        page.Find("[data-testid=handout-menu]").GetAttribute("data-help-id").ShouldBe("handout-menu");
    }
}
