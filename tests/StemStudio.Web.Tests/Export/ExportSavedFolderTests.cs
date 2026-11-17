using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Export;

/// <summary>The save confirmation names the folder path and links to the song folder (022 T036, FR-012, ui-test-ids 1.22.0).</summary>
public sealed class ExportSavedFolderTests : PageTestContext
{
    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderTrack(ExperienceLevel level)
    {
        Api.GetTrackAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns(TestData.Track());
        return Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<TrackDetail>(d => d.Add(x => x.Id, TestData.TrackId)));
    }

    internal static void ShouldBeTheFolderPath(IElement path)
    {
        path.TextContent.ShouldBe("Rick Astley > Unknown album > Never Gonna Give You Up");
        path.QuerySelectorAll("a").Select(a => (a.GetAttribute("href"), a.TextContent)).ShouldBe([
            ($"library/{LibraryData.RickArtistId}", "Rick Astley"),
            ($"library/{LibraryData.RickAlbumId}", "Unknown album"),
            ($"library/{LibraryData.RickSongId}", "Never Gonna Give You Up")]);
        path.QuerySelectorAll(".ss-folder-sep").ShouldAllBe(s => s.GetAttribute("aria-hidden") == "true");
    }

    [Fact]
    public async Task StemSave_ShowsThePath_AndOpensTheItemInItsSongFolder()
    {
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(LibraryData.Asset(status: AssetStatusDto.Queued));
        var page = RenderTrack(ExperienceLevel.Beginner);
        await page.Find("[data-testid=export-stem-bass]").ClickAsync(new());

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        var saved = page.Find("[data-testid=export-saved]");
        ShouldBeTheFolderPath(saved.QuerySelector("[data-testid=export-saved-folder]")!);
        saved.TextContent.ShouldContain("Saved to Rick Astley > Unknown album > Never Gonna Give You Up");
        page.Find("[data-testid=export-saved-open]").GetAttribute("href").ShouldBe($"library/{LibraryData.RickSongId}#asset-{AssetData.AssetId}");
    }

    [Fact]
    public async Task StemPack_LinksToTheFirstItemsSongFolder()
    {
        var first = Guid.NewGuid();
        Api.CreateStemPackAsync(Arg.Any<CreateStemPackRequest>(), Arg.Any<CancellationToken>())
            .Returns(new StemPackDto([LibraryData.Asset(first, AssetStatusDto.Queued), LibraryData.Asset(Guid.NewGuid(), AssetStatusDto.Queued)]));
        var page = RenderTrack(ExperienceLevel.Advanced);
        await page.Find("[data-testid=export-all-stems]").ClickAsync(new());

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        page.Find("[data-testid=export-saved-folder]").QuerySelectorAll("a").Count.ShouldBe(3);
        page.Find("[data-testid=export-saved-open]").GetAttribute("href").ShouldBe($"library/{LibraryData.RickSongId}#asset-{first}");
    }

    [Fact]
    public async Task WithoutPlacement_KeepsThe007Link()
    {
        Api.CreateAudioExportAsync(Arg.Any<CreateAudioExportRequest>(), Arg.Any<CancellationToken>()).Returns(AssetData.Asset(status: AssetStatusDto.Queued));
        var page = RenderTrack(ExperienceLevel.Beginner);
        await page.Find("[data-testid=export-stem-bass]").ClickAsync(new());

        await page.Find("[data-testid=export-submit]").ClickAsync(new());

        page.FindAll("[data-testid=export-saved-folder]").ShouldBeEmpty();
        page.Find("[data-testid=export-saved-open]").GetAttribute("href").ShouldBe($"library?asset={AssetData.AssetId}");
    }
}
