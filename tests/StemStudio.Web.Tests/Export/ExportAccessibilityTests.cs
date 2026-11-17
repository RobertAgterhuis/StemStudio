using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Help;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Export;

/// <summary>Accessibility and help review of feature 007 (T067): names, focus, text status, palette, help search.</summary>
public sealed class ExportAccessibilityTests : PageTestContext
{
    private static readonly HelpCatalog Catalog = HelpCatalog.FromAssembly(typeof(HelpCatalog).Assembly);

    private IRenderedComponent<TrackDetail> RenderDetail()
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.DetailTrack());
        return Render<TrackDetail>(p => p.Add(c => c.Id, TestData.TrackId));
    }

    private static void EveryControlIsNamed(AngleSharp.Dom.IElement root)
    {
        foreach (var control in root.QuerySelectorAll("button, a, input, select, audio"))
        {
            var labelled = control.Id is { Length: > 0 } id && root.QuerySelector($"label[for='{id}']") is not null;
            var name = control.GetAttribute("aria-label") ?? control.Closest("label")?.TextContent.Trim() ?? control.TextContent.Trim();
            (labelled || !string.IsNullOrWhiteSpace(name)).ShouldBeTrue($"{control.GetAttribute("data-testid")} has no accessible name");
        }
    }

    [Fact]
    public async Task ExportDialog_EveryControlIsNamed_AndFocusMovesInAndBack()
    {
        var page = RenderDetail();
        await page.Find("[data-testid=export-stem-bass]").ClickAsync(new());

        EveryControlIsNamed(page.Find("[data-testid=export-dialog]"));
        page.Find("#export-title").GetAttribute("tabindex").ShouldBe("-1");
        Interop.FocusCaptures.ShouldBe(1);
        await page.Find("[data-testid=export-cancel]").ClickAsync(new());

        Interop.FocusRestores.ShouldBe(1);
    }

    [Fact]
    public async Task OtherKeys_DoNotCloseTheExportDialog()
    {
        var page = RenderDetail();
        await page.Find("[data-testid=export-stem-bass]").ClickAsync(new());

        await page.Find("[data-testid=export-dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "Tab" });

        page.FindAll("[data-testid=export-dialog]").Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(AssetStatusDto.Queued, "Queued")]
    [InlineData(AssetStatusDto.Rendering, "Rendering 42 %")]
    [InlineData(AssetStatusDto.Ready, "Ready")]
    [InlineData(AssetStatusDto.Failed, "Failed")]
    public void LibraryRows_AreNamed_AndTheirStatusIsText(AssetStatusDto status, string text)
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(AssetData.Asset(status: status)));

        var page = Render<AllItemsView>();

        page.Find($"[data-testid=asset-status-{AssetData.AssetId}]").TextContent.ShouldBe(text);
        EveryControlIsNamed(page.Find("table"));
    }

    [Fact]
    public async Task RenameDialog_KeysAndFocus()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(AssetData.Asset()));
        Api.RenameAssetAsync(AssetData.AssetId, "New", Arg.Any<CancellationToken>()).Returns(AssetData.Asset(name: "New"));
        var page = Render<AllItemsView>();
        await page.Find($"[data-testid=asset-rename-{AssetData.AssetId}]").ClickAsync(new());

        await page.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "Escape" });
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        await page.Find($"[data-testid=asset-rename-{AssetData.AssetId}]").ClickAsync(new());
        page.Find("[data-testid=asset-rename-input]").Input("New");
        await page.Find("[data-testid=asset-rename-input]").KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        await page.Find("[role=dialog]").KeyDownAsync(new KeyboardEventArgs { Key = "x" });
        await page.Find("[data-testid=asset-rename-input]").KeyDownAsync(new KeyboardEventArgs { Key = "Enter" });

        page.Find($"[data-testid=asset-name-{AssetData.AssetId}]").TextContent.ShouldBe("New");
        (Interop.FocusCaptures, Interop.FocusRestores).ShouldBe((2, 2));
    }

    [Fact]
    public async Task CommandPalette_OffersTheLibrary()
    {
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([]);
        Api.ListMixesAsync(null, Arg.Any<CancellationToken>()).Returns([]);
        Store.Snapshot = new(UserPreferences.Default, true);
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));

        await shell.Find("[data-testid=command-palette-open]").ClickAsync(new MouseEventArgs());

        shell.WaitForAssertion(() => shell.Find("[data-testid=command-go-library]").TextContent.ShouldContain("Go to Library"));
    }

    [Theory]
    [InlineData("export", "export-dialog")]
    [InlineData("what i hear", "export-dialog")]
    [InlineData("flac", "export-formats")]
    [InlineData("download", "library")]
    [InlineData("lowered", "export-clipping")]
    [InlineData("loop region", "export-range")]
    [InlineData("export range", "export-range")]
    public void HelpSearch_FindsTheFeature007Topics(string query, string expected) =>
        new HelpSearch(Catalog).Search(query)[0].TopicId.ShouldBe(expected);

    [Fact]
    public void Feature007Topics_AreReviewedAndShort()
    {
        foreach (var id in new[] { "library", "export-dialog", "export-formats", "export-range", "export-clipping" })
        {
            var topic = Catalog.Topic(id).ShouldNotBeNull();
            // library: re-reviewed for score items (008 T075); export-dialog: practice-tempo note (012 T064).
            topic.Reviewed.ShouldBe(id switch { "library" => "008", "export-dialog" => "012", _ => "007" });
            topic.Markdown.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length.ShouldBeLessThanOrEqualTo(250, id);
        }
    }
}
