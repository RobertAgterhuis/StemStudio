using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// The folder mode of the library page (022 T046, FR-013, FR-020, FR-021; ui-test-ids 1.22.0): routes, breadcrumb,
/// header, missing folders, "Sorting your library", polling and "All items".
/// </summary>
public sealed class LibraryFolderPageTests : LibraryTestContext
{
    private static LibraryFolderViewDto AlbumView(int pending = 0, string revision = LibraryData.Revision) => LibraryData.View(
        LibraryData.Folder(LibraryData.AlbumId, "Master of Puppets", LibraryData.Cover(), items: 7, year: 1986),
        [LibraryData.Ref(LibraryData.MetallicaId, "Metallica")],
        [LibraryData.Folder(LibraryData.SongId, "Battery", LibraryData.Cover(), items: 3)],
        pending: pending,
        revision: revision);

    [Fact]
    public void Root_ShowsTheArtists_WithTheLibraryTitleAndTotal()
    {
        var page = RenderLibrary();

        var folders = page.Find("[data-testid=library-folders]");
        folders.GetAttribute("data-level").ShouldBe("root");
        page.Find("[data-testid=library-folder-title]").TagName.ShouldBe("H1");
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Library");
        page.Find("[data-testid=library-folder-counts]").TextContent.ShouldBe("8 items, total size 234 MB");
        page.Find("[data-testid=library-total-size]").TextContent.ShouldBe("234 MB");
        page.FindAll("[data-testid^=folder-tile-]").Select(t => t.GetAttribute("data-testid")).ShouldBe(
            [$"folder-tile-{LibraryData.MetallicaId}", $"folder-tile-{LibraryData.UnknownArtistId}"]);
        page.FindAll("[data-testid=library-folder-art]").ShouldBeEmpty();
        page.FindAll("[data-testid=library-filing-pending]").ShouldBeEmpty();
        page.Find("[data-testid=library-folder-title]").GetAttribute("data-help-id").ShouldBe("library-folders");
    }

    [Fact]
    public void Root_Breadcrumb_IsANavigationLandmark_WithTheRootCurrent()
    {
        var page = RenderLibrary();

        var nav = page.Find("[data-testid=library-breadcrumb]");
        (nav.TagName, nav.GetAttribute("aria-label")).ShouldBe(("NAV", "Library path"));
        nav.QuerySelector("ol").ShouldNotBeNull();
        page.Find("[data-testid=library-crumb-root]").GetAttribute("aria-current").ShouldBe("page");
    }

    [Fact]
    public void SongFolder_HasTheBreadcrumbTitleArtAndCounts()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());

        var page = RenderFolder(LibraryData.SongId);

        page.Find("[data-testid=library-folders]").GetAttribute("data-level").ShouldBe("song");
        page.Find("[data-testid=library-crumb-root]").GetAttribute("href").ShouldBe("library");
        page.Find($"[data-testid=library-crumb-{LibraryData.MetallicaId}]").GetAttribute("href").ShouldBe($"library/{LibraryData.MetallicaId}");
        page.Find($"[data-testid=library-crumb-{LibraryData.AlbumId}]").TextContent.ShouldBe("Master of Puppets");
        var current = page.Find($"[data-testid=library-crumb-{LibraryData.SongId}]");
        (current.TagName, current.GetAttribute("aria-current"), current.TextContent).ShouldBe(("SPAN", "page", "Battery"));
        page.FindAll("[aria-current=page]").Count.ShouldBe(1);
        page.Find("[data-testid=library-breadcrumb]").TextContent.ShouldContain("Library");
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Battery");
        page.Find("[data-testid=library-folder-art]").GetAttribute("data-art").ShouldBe("cover");
        page.Find("[data-testid=library-folder-counts]").TextContent.ShouldBe("3 items, 88 MB");
    }

    [Fact]
    public void DeepBreadcrumb_CollapsesItsFirstEntriesUnder480Px()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());

        var page = RenderFolder(LibraryData.SongId);

        var items = page.FindAll("[data-testid=library-breadcrumb] li");
        items.Select(li => li.ClassName ?? string.Empty).ShouldBe(["ss-crumb-collapsible", "ss-crumb-ellipsis", "ss-crumb-collapsible", string.Empty, string.Empty]);
        items[1].GetAttribute("aria-hidden").ShouldBe("true");
    }

    [Fact]
    public void AlbumFolder_ListsItsSongs_AndAskedForTheAlbumByItsId()
    {
        Folder(LibraryData.AlbumId, AlbumView());

        var page = RenderFolder(LibraryData.AlbumId);

        page.Find("[data-testid=library-folders]").GetAttribute("data-level").ShouldBe("album");
        page.Find($"[data-testid=folder-tile-{LibraryData.SongId}]").GetAttribute("data-level").ShouldBe("song");
        page.Find("[data-testid=library-folder-counts]").TextContent.ShouldBe("2 songs, 7 items, 85 MB");
        page.Find("ul.ss-folder-grid").GetAttribute("aria-label").ShouldBe("Songs");
        FolderRequests(LibraryData.AlbumId).ShouldHaveSingleItem().ShouldBe(LibraryFolderRequest.Default);
    }

    [Theory]
    [InlineData("so0000000000000000")]
    [InlineData("zz-not-a-folder")]
    public void UnknownOrEmptiedFolder_SaysItIsEmptyNow_WithLinksToTheLibraryAndTheSearch(string folderId)
    {
        var page = RenderFolder(folderId);

        var missing = page.Find("[data-testid=library-folder-missing]");
        missing.TextContent.ShouldContain("This folder is empty now");
        missing.QuerySelectorAll("a").Select(a => a.TextContent).ShouldBe(["Open the library", "search for the item"]);
        missing.QuerySelector("a")!.GetAttribute("href").ShouldBe("library");
        page.FindAll("[data-testid=library-error]").ShouldBeEmpty();
    }

    /// <summary>
    /// "Open in library" right after an export links to the song folder of the preview placement; that folder exists only
    /// once the item is filed. The view keeps asking every 2 s and shows the folder when it appears (main CI: export.spec
    /// waited 60 s for a row on a page that had stopped polling at its first 404).
    /// </summary>
    [Fact]
    public void FolderNotThereYet_IsAskedAgainEveryTwoSeconds_AndShownWhenTheItemIsFiled()
    {
        Folder(LibraryData.SongId, () => throw Problem(HttpStatusCode.NotFound, "This folder is empty now"), LibraryData.SongView);
        var page = RenderFolder(LibraryData.SongId);
        page.Find("[data-testid=library-folder-missing]").ShouldNotBeNull();

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => page.FindAll("[data-testid=library-folder-missing]").ShouldBeEmpty());
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldNotBeEmpty();
        FolderRequests(LibraryData.SongId).Count().ShouldBe(2);
    }

    [Fact]
    public void InvalidFolderId_IsNotSentToTheApi()
    {
        RenderFolder("../../etc");

        FolderRequests("../../etc").ShouldBeEmpty();
    }

    [Fact]
    public async Task MissingFolder_SearchLink_FocusesTheSearchBox()
    {
        var page = RenderFolder("so0000000000000000");

        await page.FindAll("[data-testid=library-folder-missing] a")[1].ClickAsync(new());

        JSInterop.VerifyFocusAsyncInvoke().Arguments[0].ShouldBeOfType<Microsoft.AspNetCore.Components.ElementReference>();
    }

    [Fact]
    public void FilingPending_IsShown_AndPolledEveryTwoSecondsUntilDone()
    {
        Folder(LibraryData.AlbumId, () => AlbumView(pending: 3), () => AlbumView(pending: 3), () => AlbumView(pending: 0, revision: "00000000000000ff"));
        var page = RenderFolder(LibraryData.AlbumId);
        page.Find("[data-testid=library-filing-pending]").TextContent.ShouldBe("Sorting your library (3 items left)");
        page.Find("[data-testid=library-filing-pending]").GetAttribute("role").ShouldBe("status");

        Timer.Tick();
        page.WaitForState(() => Timer.Waits >= 2);
        FolderRequests(LibraryData.AlbumId).Count().ShouldBe(1);

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();
        page.WaitForAssertion(() => FolderRequests(LibraryData.AlbumId).Count().ShouldBe(2));
        Time.Advance(FolderViewPollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => page.FindAll("[data-testid=library-filing-pending]").ShouldBeEmpty());
        KnownRevisions(LibraryData.AlbumId).ShouldBe([null, LibraryData.Revision, LibraryData.Revision]);
    }

    [Fact]
    public void UnchangedView_304_KeepsWhatIsShown()
    {
        Folder(LibraryData.AlbumId, () => AlbumView(pending: 1), () => null);
        var page = RenderFolder(LibraryData.AlbumId);

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => FolderRequests(LibraryData.AlbumId).Count().ShouldBe(2));
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Master of Puppets");
        page.Find("[data-testid=library-filing-pending]").ShouldNotBeNull();
    }

    [Fact]
    public void Maintenance503_WhilePolling_KeepsTheLastView_WithoutAnErrorBanner()
    {
        Folder(LibraryData.AlbumId, () => AlbumView(pending: 1), () => throw Maintenance(), () => AlbumView(pending: 0));
        var page = RenderFolder(LibraryData.AlbumId);

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();
        page.WaitForAssertion(() => FolderRequests(LibraryData.AlbumId).Count().ShouldBe(2));

        page.FindAll("[data-testid=library-error]").ShouldBeEmpty();
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Master of Puppets");
        Time.Advance(FolderViewPollEvery);
        Timer.Tick();
        page.WaitForAssertion(() => page.FindAll("[data-testid=library-filing-pending]").ShouldBeEmpty());
    }

    [Fact]
    public void Maintenance503_OnTheFirstLoad_ShowsLoading_AndRetries()
    {
        RootAnswers.Enqueue(() => throw Maintenance());
        RootAnswers.Enqueue(LibraryData.RootView);
        var page = RenderLibrary();
        page.FindAll("[data-testid=library-error]").ShouldBeEmpty();
        page.Find(".ss-skeleton").ShouldNotBeNull();

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => page.FindAll("[data-testid^=folder-tile-]").Count.ShouldBe(2));
    }

    [Fact]
    public void OtherFailures_ShowTheReason()
    {
        RootAnswers.Enqueue(() => throw Problem(HttpStatusCode.InternalServerError, "Broken"));

        var page = RenderLibrary();

        page.Find("[data-testid=library-error]").TextContent.ShouldBe("Broken");
    }

    [Fact]
    public void FailureAfterAView_KeepsTheViewAndShowsTheReason()
    {
        Folder(LibraryData.AlbumId, () => AlbumView(pending: 1), () => throw Problem(HttpStatusCode.InternalServerError, "Broken"));
        var page = RenderFolder(LibraryData.AlbumId);

        Time.Advance(FolderViewPollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => page.Find("[data-testid=library-error]").TextContent.ShouldBe("Broken"));
        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Master of Puppets");
    }

    [Fact]
    public void EmptyLibrary_ShowsTheEmptyState_AndDoesNotPoll()
    {
        RootAnswers.Enqueue(() => LibraryData.View(null));

        var page = RenderLibrary();

        page.Find("[data-testid=library-empty]").TextContent.ShouldContain("No items yet");
        Timer.Waits.ShouldBe(0);
    }

    [Fact]
    public void AllItems_AtAdvanced_KeepsThe007ListAndFilters_WithEveryItemsPath()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(LibraryData.Asset()));

        var page = RenderLibrary("library?view=all");

        page.FindAll("[data-testid=library-folders]").ShouldBeEmpty();
        page.Find("[data-testid=library-filter-track]").ShouldNotBeNull();
        page.Find("[data-testid=library-search]").ShouldNotBeNull();
        page.Find("[data-testid=library-view-folders]").GetAttribute("href").ShouldBe("library");
        page.Find($"[data-testid=asset-path-{AssetData.AssetId}]").QuerySelectorAll("a").Count.ShouldBe(3);
        page.Find("[data-testid=library-total-size]").ShouldNotBeNull();
    }

    [Fact]
    public void AllItems_AtBeginner_ShowsTheFolders()
    {
        var page = RenderLibrary("library?view=all", ExperienceLevel.Beginner);

        page.Find("[data-testid=library-folders]").ShouldNotBeNull();
        page.FindAll("[data-testid=library-filter-track]").ShouldBeEmpty();
    }

    [Fact]
    public void ViewAllLink_OpensAllItems()
    {
        var page = RenderLibrary();

        page.Find("[data-testid=library-view-all]").GetAttribute("href").ShouldBe("library?view=all");
    }

    [Fact]
    public void LocationChange_ToAnotherFolder_LoadsIt()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());
        var page = RenderLibrary();

        Navigation.NavigateTo($"library/{LibraryData.SongId}");
        page.Render(p => p.Add(c => c.Value, new ExperienceContext(ExperienceLevel.Advanced, StemStudio.Web.Preferences.DensityPreference.Comfortable))
            .AddChildContent<StemStudio.Web.Components.Library.LibraryPage>(c => c.Add(x => x.FolderId, LibraryData.SongId)));

        page.WaitForAssertion(() => page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Battery"));
    }

    private static TimeSpan FolderViewPollEvery => StemStudio.Web.Components.Library.FolderView.PollEvery;

    private static ApiException Maintenance() => new(new ApiError(HttpStatusCode.ServiceUnavailable, "The library is being restored", null, new Dictionary<string, string[]>())
    {
        Type = "https://stemstudio.local/problems/library-maintenance",
    });
}
