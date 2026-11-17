using System.Text.RegularExpressions;
using AngleSharp.Dom;
using Bunit;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>Folder tiles, folder art and the grid/list choice (022 T048, FR-014..FR-016, FR-019, R-05; WCAG 2.2 AA).</summary>
public sealed partial class FolderTileTests : LibraryTestContext
{
    private const string PublicBase = "http://localhost:8081";

    private static string AccessibleName(IElement link) => Spaces().Replace(link.TextContent, " ").Trim();

    [Fact]
    public void EveryTile_IsOneLink_NamedWithNameLevelAndCounts_ThatContainsTheVisibleText()
    {
        var page = RenderLibrary();

        var tile = page.Find($"[data-testid=folder-tile-{LibraryData.MetallicaId}]");
        (tile.TagName, tile.GetAttribute("href")).ShouldBe(("A", $"library/{LibraryData.MetallicaId}"));
        (tile.GetAttribute("data-level"), tile.GetAttribute("data-art")).ShouldBe(("artist", "mosaic"));
        tile.HasAttribute("aria-label").ShouldBeFalse();
        AccessibleName(tile).ShouldBe("Metallica, artist, 2 albums, 7 items");
        tile.QuerySelectorAll("a").ShouldBeEmpty();
        page.Find($"[data-testid=folder-name-{LibraryData.MetallicaId}]").TextContent.ShouldBe("Metallica");
        page.Find($"[data-testid=folder-counts-{LibraryData.MetallicaId}]").TextContent.ShouldBe("2 albums, 7 items");
        page.FindAll("li.ss-folder-item > a").Count.ShouldBe(2);
    }

    [Fact]
    public void Mosaic_HasTwoDecorativeLazyThumbs_WithFixedSize_AndTwoToneCells()
    {
        var page = RenderLibrary();

        var art = page.Find($"[data-testid=folder-art-{LibraryData.MetallicaId}]");
        art.GetAttribute("data-art").ShouldBe("mosaic");
        art.ClassList.ShouldContain("ss-art-tone-3");
        var images = art.QuerySelectorAll("img");
        images.Length.ShouldBe(2);
        images.ShouldAllBe(img => img.GetAttribute("alt") == string.Empty && img.GetAttribute("loading") == "lazy" && img.GetAttribute("decoding") == "async"
            && img.GetAttribute("width") == "80" && img.GetAttribute("height") == "80");
        images[0].GetAttribute("src").ShouldBe($"{PublicBase}/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/artwork/thumb?v=4f2a9c0e1b3d");
        art.QuerySelectorAll(".ss-folder-art-cell").ShouldAllBe(cell => cell.GetAttribute("aria-hidden") == "true");
        art.QuerySelectorAll(".ss-folder-art-cell").Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(3, 3, 1)]
    [InlineData(4, 4, 0)]
    public void Mosaic_HasUpToFourCells(int images, int pictures, int empty)
    {
        RootAnswers.Enqueue(() => LibraryData.View(null, children: [LibraryData.Folder(LibraryData.MetallicaId, "Metallica", LibraryData.Cover(images))]));

        var art = RenderLibrary().Find($"[data-testid=folder-art-{LibraryData.MetallicaId}]");

        art.QuerySelectorAll("img").Length.ShouldBe(pictures);
        art.QuerySelectorAll(".ss-folder-art-cell").Length.ShouldBe(empty);
    }

    [Fact]
    public void Cover_HasSrcsetWithTheLargePicture_AndSizes()
    {
        RootAnswers.Enqueue(() => LibraryData.View(null, children: [LibraryData.Folder(LibraryData.AlbumId, "Master of Puppets", LibraryData.Cover(), year: 1986)]));

        var page = RenderLibrary();

        var img = page.Find($"[data-testid=folder-art-{LibraryData.AlbumId}] img");
        img.GetAttribute("srcset").ShouldBe($"{PublicBase}/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b01/artwork/thumb?v=4f2a 160w, {PublicBase}/api/tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b01/artwork/cover?v=4f2a 600w");
        (img.GetAttribute("sizes"), img.GetAttribute("width"), img.GetAttribute("height"), img.GetAttribute("alt")).ShouldBe(("160px", "160", "160", string.Empty));
        page.Find($"[data-testid=folder-year-{LibraryData.AlbumId}]").TextContent.ShouldBe("1986");
        AccessibleName(page.Find($"[data-testid=folder-tile-{LibraryData.AlbumId}]")).ShouldBe("Master of Puppets, album, 1986, 2 songs, 1 item");
    }

    [Fact]
    public void YearExists_OnlyForAlbumsWithAYear()
    {
        var page = RenderLibrary();

        page.FindAll("[data-testid^=folder-year-]").ShouldBeEmpty();
    }

    [Fact]
    public void Placeholder_ShowsTheGlyphHiddenFromScreenReaders_OnItsTone()
    {
        var page = RenderLibrary();

        var art = page.Find($"[data-testid=folder-art-{LibraryData.UnknownArtistId}]");
        art.GetAttribute("data-art").ShouldBe("placeholder");
        art.ClassList.ShouldContain("ss-art-tone-0");
        var glyph = art.QuerySelector(".ss-folder-art-glyph")!;
        (glyph.TextContent, glyph.GetAttribute("aria-hidden")).ShouldBe(("?", "true"));
        art.QuerySelectorAll("img").ShouldBeEmpty();
    }

    [Fact]
    public async Task BrokenCover_FallsBackToThePlaceholder()
    {
        RootAnswers.Enqueue(() => LibraryData.View(null, children: [LibraryData.Folder(LibraryData.AlbumId, "Master of Puppets", LibraryData.Cover())]));
        var page = RenderLibrary();

        await page.Find($"[data-testid=folder-art-{LibraryData.AlbumId}] img").TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        var art = page.Find($"[data-testid=folder-art-{LibraryData.AlbumId}]");
        art.GetAttribute("data-art").ShouldBe("placeholder");
        art.QuerySelector(".ss-folder-art-glyph")!.TextContent.ShouldBe("M");
    }

    [Fact]
    public async Task BrokenMosaicPicture_BecomesAToneCell()
    {
        var page = RenderLibrary();

        await page.FindAll($"[data-testid=folder-art-{LibraryData.MetallicaId}] img")[1].TriggerEventAsync("onerror", new Microsoft.AspNetCore.Components.Web.ErrorEventArgs());

        var art = page.Find($"[data-testid=folder-art-{LibraryData.MetallicaId}]");
        art.QuerySelectorAll("img").Length.ShouldBe(1);
        art.QuerySelectorAll(".ss-folder-art-cell").Length.ShouldBe(3);
    }

    [Fact]
    public void HeaderArt_IsDecorative_AndCarriesTheArtHelpMarker()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());

        var page = RenderFolder(LibraryData.SongId);

        var art = page.Find("[data-testid=library-folder-art]");
        art.QuerySelector("img")!.GetAttribute("alt").ShouldBe(string.Empty);
        art.ParentElement!.GetAttribute("data-help-id").ShouldBe("library-art");
    }

    [Fact]
    public async Task LayoutToggle_SwitchesToTheList_AndIsStoredOnThisDevice()
    {
        var page = RenderLibrary();
        page.Find("[data-testid=library-layout-grid]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("[data-testid=library-layout-list]").GetAttribute("aria-pressed").ShouldBe("false");

        await page.Find("[data-testid=library-layout-list]").ClickAsync(new());

        page.Find("[data-testid=library-layout-list]").GetAttribute("aria-pressed").ShouldBe("true");
        page.Find("ul.ss-folder-list").QuerySelectorAll("li").Length.ShouldBe(2);
        page.FindAll("ul.ss-folder-grid").ShouldBeEmpty();
        page.Find($"[data-testid=folder-tile-{LibraryData.MetallicaId}] .ss-folder-size").TextContent.ShouldBe("205 MB");
        page.Find($"[data-testid=folder-tile-{LibraryData.MetallicaId}] img").GetAttribute("width").ShouldBe("64");
        var stored = JSInterop.Invocations.Where(i => i.Identifier == "localStorage.setItem").ShouldHaveSingleItem();
        stored.Arguments.ShouldBe([LibraryViewPreferences.StorageKey, "{\"layout\":\"list\",\"sorts\":{}}"]);
    }

    [Fact]
    public void StoredListLayout_IsUsed()
    {
        JSInterop.Setup<string?>("localStorage.getItem", LibraryViewPreferences.StorageKey).SetResult("{\"layout\":\"list\",\"sorts\":{\"album\":\"recent\",\"bogus\":\"name\",\"song\":\"nope\"}}");

        var page = RenderLibrary();

        page.Find("[data-testid=library-layout-list]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("null")]
    [InlineData("{\"layout\":7}")]
    public void BrokenStoredValue_FallsBackToTheGrid(string stored)
    {
        JSInterop.Setup<string?>("localStorage.getItem", LibraryViewPreferences.StorageKey).SetResult(stored);

        var page = RenderLibrary();

        page.Find("[data-testid=library-layout-grid]").GetAttribute("aria-pressed").ShouldBe("true");
    }

    [Fact]
    public void Compact_Uses128PxTiles()
    {
        Navigation.NavigateTo("library");
        var page = Render<Microsoft.AspNetCore.Components.CascadingValue<StemStudio.Web.Experience.ExperienceContext>>(p => p
            .Add(c => c.Value, new StemStudio.Web.Experience.ExperienceContext(StemStudio.Web.Experience.ExperienceLevel.Advanced, StemStudio.Web.Preferences.DensityPreference.Compact))
            .AddChildContent<LibraryPage>());

        page.FindAll($"[data-testid=folder-art-{LibraryData.MetallicaId}] img").ShouldAllBe(img => img.GetAttribute("width") == "64");
    }

    [Fact]
    public void LayoutToggles_AreButtonsInANamedGroup_WithTheVisibleTextAsName()
    {
        var page = RenderLibrary();

        var group = page.Find("[data-testid=library-layout-grid]").ParentElement!;
        (group.GetAttribute("role"), group.GetAttribute("aria-label")).ShouldBe(("group", "Layout"));
        page.Find("[data-testid=library-layout-grid]").TagName.ShouldBe("BUTTON");
        page.Find("[data-testid=library-layout-grid]").TextContent.ShouldBe("Grid");
        page.Find("[data-testid=library-layout-grid]").GetAttribute("type").ShouldBe("button");
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
