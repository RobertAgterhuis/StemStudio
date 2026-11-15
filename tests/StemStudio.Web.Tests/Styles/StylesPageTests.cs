using System.Net;
using AngleSharp.Dom;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Styles;
using StemStudio.Web.Components.Styles;
using StemStudio.Web.Styles;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Styles;

/// <summary>The style library page (009 T048, US2-1..3, FR-003, FR-004; ui-test-ids 1.8.0 style-*).</summary>
public sealed class StylesPageTests : PageTestContext
{
    private static IEnumerable<string?> RowIds(IRenderedComponent<StylesPage> page) =>
        page.FindAll("[data-testid^=style-row-]").Select(r => r.GetAttribute("data-testid"));

    private static Task Choose(IElement select, string label) =>
        select.ChangeAsync(new() { Value = select.QuerySelectorAll("option").Single(o => o.TextContent == label).GetAttribute("value") });

    [Fact]
    public void Page_ListsEveryStyle_InTheLibraryOrder_WithItsKindAndGenre()
    {
        var page = Render<StylesPage>();

        page.Find("h1").TextContent.ShouldBe("Styles");
        page.Find("[data-help-id=styles-library]").ShouldNotBeNull();
        RowIds(page).ShouldBe([$"style-row-{GenerationData.JazzId}", $"style-row-{GenerationData.SynthwaveId}", $"style-row-{GenerationData.UserStyleId}"]);
        var jazz = page.Find($"[data-testid=style-row-{GenerationData.JazzId}]");
        jazz.GetAttribute("data-kind").ShouldBe("preset");
        foreach (var text in new[] { "Jazz trio", "Preset", "Jazz", "Swing", "relaxed, warm", "piano, double bass, brushed drums", "Balanced", "Keeps the vocals" })
        {
            jazz.TextContent.ShouldContain(text);
        }

        var own = page.Find($"[data-testid=style-row-{GenerationData.UserStyleId}]");
        (own.GetAttribute("data-kind"), own.TextContent.Contains("Own style", StringComparison.Ordinal)).ShouldBe(("user", true));
        page.Find($"[data-testid=style-row-{GenerationData.SynthwaveId}]").TextContent.ShouldContain("Instrumental (no vocals)");
    }

    [Fact]
    public void Filters_AreNativeSelects_WithAnAllOption()
    {
        var page = Render<StylesPage>();

        page.Find("[data-testid=style-filter-genre]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(
            ["All", "Rock", "Pop", "Jazz", "Blues", "Electronic", "Hip hop", "Latin", "Folk", "Classical", "World", "Country", "Metal", "Soul", "Ambient"]);
        page.Find("[data-testid=style-filter-mood]").QuerySelectorAll("option").Select(o => o.TextContent)
            .ShouldBe(["All", "dreamy", "energetic", "nostalgic", "relaxed", "warm"]);
        page.Find("[data-testid=style-filter-instrument]").QuerySelectorAll("option").Select(o => o.TextContent)
            .ShouldBe(["All", "analog synth", "brushed drums", "double bass", "drum machine", "nylon guitar", "piano"]);
        page.Find("[data-testid=style-filter-kind]").QuerySelectorAll("option").Select(o => o.TextContent).ShouldBe(["All", "Presets", "Mine"]);
        page.Find("[data-testid=style-search]").GetAttribute("type").ShouldBe("search");
    }

    [Fact]
    public async Task Filters_NarrowTheList_AndAllWidensItAgain()
    {
        var page = Render<StylesPage>();

        await Choose(page.Find("[data-testid=style-filter-mood]"), "warm");
        RowIds(page).ShouldBe([$"style-row-{GenerationData.JazzId}", $"style-row-{GenerationData.UserStyleId}"]);
        await Choose(page.Find("[data-testid=style-filter-kind]"), "Mine");
        RowIds(page).ShouldBe([$"style-row-{GenerationData.UserStyleId}"]);
        await Choose(page.Find("[data-testid=style-filter-kind]"), "Presets");
        await Choose(page.Find("[data-testid=style-filter-mood]"), "All");
        await Choose(page.Find("[data-testid=style-filter-genre]"), "Electronic");
        RowIds(page).ShouldBe([$"style-row-{GenerationData.SynthwaveId}"]);
        await Choose(page.Find("[data-testid=style-filter-genre]"), "All");
        await Choose(page.Find("[data-testid=style-filter-instrument]"), "piano");
        RowIds(page).ShouldBe([$"style-row-{GenerationData.JazzId}"]);
    }

    [Theory]
    [InlineData("jazz TRIO", "5d1c3b2a-0f9e-4d8c-b7a6-958473625140")]
    [InlineData("gated drum", "6e2d4c3b-1a0f-4e9d-8c7b-a69584736251")]
    [InlineData("NYLON", "7f3e5d4c-2b1a-4f0e-9d8c-b7a695847362")]
    [InlineData("bossa", "7f3e5d4c-2b1a-4f0e-9d8c-b7a695847362")]
    public async Task Search_MatchesNameDescriptionSubGenreAndTags(string text, string expectedId)
    {
        var page = Render<StylesPage>();

        await page.Find("[data-testid=style-search]").InputAsync(new() { Value = text });

        RowIds(page).ShouldBe([$"style-row-{expectedId}"]);
    }

    [Fact]
    public async Task NoMatch_ShowsTheEmptyState()
    {
        var page = Render<StylesPage>();

        await page.Find("[data-testid=style-search]").InputAsync(new() { Value = "no such style" });

        page.FindAll("[data-testid^=style-row-]").ShouldBeEmpty();
        page.Find("[data-testid=style-empty]").TextContent.ShouldContain("No styles match");
    }

    [Fact]
    public void LoadFailure_IsShown()
    {
        Api.ListStylesAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));

        var page = Render<StylesPage>();

        page.Find("[data-testid=styles-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public void Filter_IsInactive_UntilSomethingIsChosen()
    {
        var filter = new StyleFilter();

        filter.IsActive.ShouldBeFalse();
        filter.Apply(GenerationData.Styles()).Count.ShouldBe(3);
        filter.Search = "  ";
        filter.IsActive.ShouldBeFalse();
        filter.Kind = StyleKindDto.User;
        filter.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void StyleText_NamesEveryValue()
    {
        StyleText.Genres.Select(StyleText.Genre).Distinct().Count().ShouldBe(14);
        StyleText.Tempos.Select(StyleText.Tempo).ShouldBe(["Follow the source", "Laid back", "Driving", "Half time", "Swing"]);
        StyleText.Strengths.Select(StyleText.Strength).ShouldBe(["Subtle", "Light", "Balanced", "Strong", "Free"]);
    }
}
