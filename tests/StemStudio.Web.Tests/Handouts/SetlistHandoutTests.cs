using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>
/// Lesson handout per instrument (T085, US4 scenarios 1-3, FR-009; ui-test-ids setlist-handout-*): the menu next to 014's
/// <c>setlist-pdf</c>, one click with the setlist source, "Customise..." at Advanced+ opening the same dialog without create
/// links, and no handout for an empty setlist.
/// </summary>
public sealed class SetlistHandoutTests : PageTestContext
{
    private static readonly Guid Id = PracticeFlowData.SetlistId;

    public SetlistHandoutTests()
    {
        Api.GetSetlistAsync(Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>()).Returns(InsightsData.Handout());
        Api.GetHandoutOptionsAsync(Arg.Any<HandoutSourceDto>(), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>())
            .Returns(InsightsData.BassOptions() with { Kind = HandoutKindDto.Setlist, Songs = 2 });
    }

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderEditor(ExperienceLevel level = ExperienceLevel.Advanced) =>
        RenderAtLevel<SetlistEditor>(level, p => p.Add(c => c.SetlistId, Id));

    [Fact]
    public void Menu_SitsNextToTheStandPdf()
    {
        var page = RenderEditor(ExperienceLevel.Beginner);

        var menu = page.Find("[data-testid=setlist-handout-menu]");
        menu.TextContent.ShouldContain("Lesson handout per instrument");
        menu.GetAttribute("data-help-id").ShouldBe("setlist-handout-menu");
        page.Find("[data-testid=setlist-pdf]").ShouldNotBeNull();
        page.Find(".ss-section-nav").QuerySelector("[data-testid=setlist-handout-menu]").ShouldNotBeNull();
    }

    [Fact]
    public async Task OneClick_CreatesTheSetlistHandout()
    {
        var page = RenderEditor(ExperienceLevel.Beginner);
        page.Find("[data-testid=setlist-handout-menu]").Click();

        page.FindAll("[data-testid=setlist-handout-customise]").ShouldBeEmpty();
        await page.Find("[data-testid=setlist-handout-family-overview]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(
            Arg.Is<CreateHandoutRequest>(r => r.Source.SetlistId == Id && r.Source.TrackId == null && r.Family == HandoutFamilyDto.Overview),
            Arg.Any<CancellationToken>());
        page.Find("[data-testid=handout-saved]").ShouldNotBeNull();
    }

    [Fact]
    public void Customise_OpensTheDialogForTheSetlist_WithoutCreateLinks()
    {
        var page = RenderEditor(ExperienceLevel.Expert);
        page.Find("[data-testid=setlist-handout-menu]").Click();

        page.Find("[data-testid=setlist-handout-customise]").Click();

        page.Find("[data-testid=handout-dialog]").ShouldNotBeNull();
        page.FindAll("[data-testid^=handout-ingredient-action-]").ShouldBeEmpty();
        Api.Received().GetHandoutOptionsAsync(Arg.Is<HandoutSourceDto>(s => s.SetlistId == Id), Arg.Any<HandoutFamilyDto>(), Arg.Any<TranspositionDto?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void EmptySetlist_HasNoHandoutMenu()
    {
        Api.GetSetlistAsync(Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist([]));

        var page = RenderEditor();

        page.FindAll("[data-testid=setlist-handout-menu]").ShouldBeEmpty();
        page.Find("[data-testid=setlist-pdf]").ShouldNotBeNull();
    }
}
