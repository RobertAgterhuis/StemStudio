using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Handouts;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.InstrumentViews;

namespace StemStudio.Web.Tests.Handouts;

/// <summary>
/// Handout items in the library (T078, FR-014; ui-test-ids asset-handout-*, library-filter-handout): preview, PDF download,
/// "Not included", warnings (Advanced+), "Make again" with the recipe, and the "Handouts" filter chip.
/// </summary>
public sealed class HandoutAssetRowTests : PageTestContext
{
    private static readonly Guid Id = InsightsData.HandoutId;

    private static AssetDto Ready() => InsightsData.Handout(AssetStatusDto.Ready, InsightsData.Facts()) with
    {
        Provenance = AssetData.Provenance(contents: "Bass handout") with { Origin = AssetOriginDto.Handout, HandoutRecipe = Recipe() },
    };

    private static HandoutRecipeDto Recipe() => new()
    {
        Kind = HandoutRecipeDto.RecipeKind,
        KindLabel = HandoutKindDto.Song,
        TrackId = TestData.TrackId,
        SetlistId = null,
        SetlistName = null,
        Family = HandoutFamilyDto.Bass,
        Include = null,
        NotationAssetIds = null,
        Transposition = null,
        Paper = PaperDto.A4,
    };

    private IRenderedComponent<Microsoft.AspNetCore.Components.CascadingValue<ExperienceContext>> RenderTable(AssetDto asset, ExperienceLevel level = ExperienceLevel.Advanced) =>
        RenderAtLevel<AssetTable>(level, p => p.Add(c => c.Items, [asset]));

    [Fact]
    public void ReadyHandout_HasPreviewDownloadAndFacts()
    {
        var page = RenderTable(Ready());

        var row = page.Find($"[data-testid=asset-handout-{Id}]");
        row.GetAttribute("data-family").ShouldBe("bass");
        row.TextContent.ShouldContain("Bass · 4 pages · A4");
        page.Find($"[data-testid=asset-preview-{Id}]").GetAttribute("title").ShouldBe("Handout preview: Never Gonna Give You Up - Bass handout");
        page.Find($"[data-testid=asset-preview-{Id}]").GetAttribute("src")!.ShouldContain($"/api/assets/{Id}/files/pdf?inline=true");
        var download = page.Find($"[data-testid=asset-download-pdf-{Id}]");
        download.HasAttribute("download").ShouldBeTrue();
        download.GetAttribute("href").ShouldEndWith($"/api/assets/{Id}/files/pdf");
        page.Find($"[data-testid=asset-handout-omitted-{Id}]").TextContent.ShouldBe("Not included: practice notes (not available yet)");
        page.Find($"[data-testid=asset-handout-warnings-{Id}]").TextContent.ShouldContain("scaled to fit");
    }

    [Fact]
    public void Beginner_DoesNotSeeTheWarnings()
    {
        var page = RenderTable(Ready(), ExperienceLevel.Beginner);

        page.FindAll($"[data-testid=asset-handout-warnings-{Id}]").ShouldBeEmpty();
        page.Find($"[data-testid=asset-handout-omitted-{Id}]").ShouldNotBeNull();
    }

    [Fact]
    public async Task MakeAgain_PostsTheRecipe_AndReloadsTheList()
    {
        var reloaded = false;
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>()).Returns(InsightsData.Handout());
        var page = RenderAtLevel<AssetTable>(ExperienceLevel.Beginner, p => p.Add(c => c.Items, [Ready()]).Add(c => c.OnReload, () => reloaded = true));

        await page.Find($"[data-testid=asset-handout-again-{Id}]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(Arg.Is<CreateHandoutRequest>(r =>
            r.Source.TrackId == TestData.TrackId && r.Source.SetlistId == null && r.Family == HandoutFamilyDto.Bass && r.Paper == PaperDto.A4 && r.Include == null),
            Arg.Any<CancellationToken>());
        reloaded.ShouldBeTrue();
    }

    [Fact]
    public async Task MakeAgain_OfASetlistHandout_UsesTheSetlist()
    {
        Api.CreateHandoutAsync(Arg.Any<CreateHandoutRequest>(), Arg.Any<CancellationToken>()).Returns(InsightsData.Handout());
        var setlist = Ready() with
        {
            Provenance = Ready().Provenance with
            {
                HandoutRecipe = Recipe() with { KindLabel = HandoutKindDto.Setlist, TrackId = null, SetlistId = InsightsData.SetlistId, SetlistName = "Friday gig" },
            },
        };
        var page = RenderTable(setlist);

        await page.Find($"[data-testid=asset-handout-again-{Id}]").ClickAsync(new());

        await Api.Received(1).CreateHandoutAsync(Arg.Is<CreateHandoutRequest>(r => r.Source.SetlistId == InsightsData.SetlistId && r.Source.TrackId == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void QueuedHandout_ShowsNoPreview_ButItsFamily()
    {
        var queued = InsightsData.Handout() with { Provenance = Ready().Provenance };

        var page = RenderTable(queued);

        page.Find($"[data-testid=asset-handout-{Id}]").GetAttribute("data-family").ShouldBe("bass");
        page.FindAll($"[data-testid=asset-preview-{Id}], [data-testid=asset-download-pdf-{Id}], [data-testid=asset-handout-again-{Id}]").ShouldBeEmpty();
    }

    [Fact]
    public void FailedHandout_CanBeMadeAgain()
    {
        var failed = InsightsData.Handout(AssetStatusDto.Failed) with { Provenance = Ready().Provenance };

        var page = RenderTable(failed);

        page.Find($"[data-testid=asset-handout-again-{Id}]").TextContent.Trim().ShouldEndWith("Make again");
    }

    [Fact]
    public void Accessibility_ActionsAreNamedWithTheirVisibleText()
    {
        var page = RenderTable(Ready());

        InstrumentViewsAccessibility.EveryControlIsNamedWithItsVisibleText(page.Find($"[data-testid=asset-handout-{Id}]"));
    }

    [Fact]
    public void HandoutChip_FiltersTheListToHandouts_AtEveryLevel()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(AssetData.Asset(), Ready()));
        var page = RenderAtLevel<AllItemsView>(ExperienceLevel.Beginner, _ => { });

        var chip = page.Find("[data-testid=library-filter-handout]");
        (chip.TextContent.Trim(), chip.GetAttribute("aria-pressed")).ShouldBe(("Handouts", "false"));
        chip.Click();

        page.Find("[data-testid=library-filter-handout]").GetAttribute("aria-pressed").ShouldBe("true");
        page.FindAll("[data-testid^=asset-row-]").Select(r => r.GetAttribute("data-testid")).ShouldBe([$"asset-row-{Id}"]);
        page.Find("[data-testid=library-filter-handout]").Click();
        page.FindAll("[data-testid^=asset-row-]").Count.ShouldBe(2);
    }
}
