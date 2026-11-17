using Bunit;
using Microsoft.AspNetCore.Components;
using StemStudio.Contracts.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>Journey levels R7-R9 for the 022 ids and the Advanced order select (022 T052, FR-018, FR-035).</summary>
public sealed class LibraryLevelTests : LibraryTestContext
{
    private static readonly string[] AdvancedOnly = ["library-sort", "library-view-all", "library-search-scope"];

    private static readonly string[] EveryLevel = ["library-folders", "library-breadcrumb", "library-folder-title", "library-folder-counts", "library-layout-grid",
        "library-layout-list", "library-search", "library-search-status"];

    private static LibraryFolderViewDto ArtistView() => LibraryData.View(
        LibraryData.Folder(LibraryData.MetallicaId, "Metallica", LibraryData.Cover(2), items: 7),
        children: [LibraryData.Folder(LibraryData.AlbumId, "Master of Puppets", LibraryData.Cover(), items: 7, year: 1986)]);

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void EveryLevel_SeesFoldersArtBreadcrumbSearchAndLayout(ExperienceLevel level)
    {
        Folder(LibraryData.MetallicaId, ArtistView());

        var page = RenderFolder(LibraryData.MetallicaId, level);

        foreach (var id in EveryLevel)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }

        page.Find("[data-testid=library-folder-art]").ShouldNotBeNull();
        page.FindAll("[data-testid^=folder-tile-]").Count.ShouldBe(1);
    }

    [Fact]
    public void Beginner_HidesSortAllItemsAndScope()
    {
        Folder(LibraryData.MetallicaId, ArtistView());

        var page = RenderFolder(LibraryData.MetallicaId, ExperienceLevel.Beginner);

        foreach (var id in AdvancedOnly)
        {
            page.FindAll($"[data-testid={id}]").ShouldBeEmpty(id);
        }
    }

    [Theory]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public void Advanced_SeesSortAllItemsAndScope(ExperienceLevel level)
    {
        Folder(LibraryData.MetallicaId, ArtistView());

        var page = RenderFolder(LibraryData.MetallicaId, level);

        foreach (var id in AdvancedOnly)
        {
            page.FindAll($"[data-testid={id}]").Count.ShouldBe(1, id);
        }
    }

    [Fact]
    public void Scope_ExistsOnlyInsideAFolder()
    {
        var page = RenderLibrary();

        page.FindAll("[data-testid=library-search-scope]").ShouldBeEmpty();
    }

    [Fact]
    public void Sort_OfAlbums_OffersYearFirst_AsTheDefault()
    {
        Folder(LibraryData.MetallicaId, ArtistView());

        var page = RenderFolder(LibraryData.MetallicaId);

        var select = page.Find("[data-testid=library-sort]");
        (select.TagName, select.Id).ShouldBe(("SELECT", "library-sort"));
        page.Find("label[for=library-sort]").TextContent.ShouldBe("Order");
        select.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["year", "name", "recent", "size", "items"]);
        select.QuerySelector("option[selected]")!.GetAttribute("value").ShouldBe("year");
        FolderRequests(LibraryData.MetallicaId).ShouldHaveSingleItem().Sort.ShouldBeNull();
    }

    [Fact]
    public void Sort_OfArtists_HasNoYear_AndDefaultsToName()
    {
        var page = RenderLibrary();

        var select = page.Find("[data-testid=library-sort]");
        select.QuerySelectorAll("option").Select(o => o.GetAttribute("value")).ShouldBe(["name", "recent", "size", "items"]);
        select.QuerySelector("option[selected]")!.GetAttribute("value").ShouldBe("name");
    }

    [Fact]
    public async Task ChangingTheSort_ReloadsInThatOrder_AndRemembersItPerLevel()
    {
        Folder(LibraryData.MetallicaId, ArtistView());
        var page = RenderFolder(LibraryData.MetallicaId);

        await page.Find("[data-testid=library-sort]").ChangeAsync(new ChangeEventArgs { Value = "recent" });

        FolderRequests(LibraryData.MetallicaId).Select(r => r.Sort).ShouldBe([null, FolderSortDto.Recent]);
        page.Find("[data-testid=library-sort] option[selected]").GetAttribute("value").ShouldBe("recent");
        JSInterop.Invocations.Where(i => i.Identifier == "localStorage.setItem").ShouldHaveSingleItem()
            .Arguments[1].ShouldBe("{\"layout\":\"grid\",\"sorts\":{\"album\":\"recent\"}}");
    }

    [Fact]
    public async Task UnknownSortValue_IsIgnored()
    {
        Folder(LibraryData.MetallicaId, ArtistView());
        var page = RenderFolder(LibraryData.MetallicaId);

        await page.Find("[data-testid=library-sort]").ChangeAsync(new ChangeEventArgs { Value = "42" });

        FolderRequests(LibraryData.MetallicaId).Count().ShouldBe(1);
    }

    [Fact]
    public void StoredSort_IsSentForThatLevel()
    {
        JSInterop.Setup<string?>("localStorage.getItem", LibraryViewPreferences.StorageKey).SetResult("{\"layout\":\"grid\",\"sorts\":{\"album\":\"size\",\"artist\":\"items\"}}");
        Folder(LibraryData.MetallicaId, ArtistView());

        RenderFolder(LibraryData.MetallicaId);

        FolderRequests(LibraryData.MetallicaId).ShouldHaveSingleItem().Sort.ShouldBe(FolderSortDto.Size);
    }

    [Fact]
    public void LibrarySearch_IsALabelledSearchField_AtBeginner()
    {
        var page = RenderLibrary(level: ExperienceLevel.Beginner);

        var box = page.Find("[data-testid=library-search]");
        (box.TagName, box.GetAttribute("type"), box.Id, box.GetAttribute("maxlength")).ShouldBe(("INPUT", "search", "library-search", "100"));
        page.Find("label[for=library-search]").TextContent.ShouldBe("Search the library");
        box.ParentElement!.ParentElement!.GetAttribute("role").ShouldBe("search");
        box.ParentElement!.ParentElement!.GetAttribute("data-help-id").ShouldBe("library-search");
    }
}
