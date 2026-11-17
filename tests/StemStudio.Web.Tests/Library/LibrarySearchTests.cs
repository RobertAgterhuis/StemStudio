using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Library;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Interop;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// The live search on the library page (022 T066, T068; FR-022, FR-025..FR-029; ui-test-ids 1.22.0): groups, marks, paths,
/// item actions, no matches, failures, the announced count, keyboard and the <c>/</c> shortcut.
/// </summary>
public sealed class LibrarySearchTests : LibraryTestContext
{
    private static readonly Guid SnareId = Guid.Parse("7d4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");

    private static FolderHitDto ArtistHit() => new()
    {
        Folder = LibraryData.Folder(LibraryData.MetallicaId, "Metallica", LibraryData.Cover(2), items: 7),
        Highlights = [new HighlightDto { Field = SearchFieldDto.Name, Start = 0, Length = 3 }],
    };

    private static FolderHitDto SongHit() => new()
    {
        Folder = LibraryData.Folder(LibraryData.SongId, "Battery", items: 3),
        Path = [LibraryData.Ref(LibraryData.MetallicaId, "Metallica"), LibraryData.Ref(LibraryData.AlbumId, "Master of Puppets")],
    };

    private static ItemHitDto ItemHit(Guid? id = null, string name = "Battery - snare") => new()
    {
        Asset = LibraryData.Asset(id ?? SnareId, name: name),
        Path = [LibraryData.Ref(LibraryData.RickArtistId, "Rick Astley"), LibraryData.Ref(LibraryData.RickAlbumId, "Unknown album"), LibraryData.Ref(LibraryData.RickSongId, "Never Gonna Give You Up")],
        Highlights = [new HighlightDto { Field = SearchFieldDto.Name, Start = 10, Length = 5 }, new HighlightDto { Field = SearchFieldDto.Instrument, Start = 0, Length = 5, Label = "Snare" },
            new HighlightDto { Field = SearchFieldDto.Artist, Start = 0, Length = 4 }],
        MatchedInstruments = ["Snare"],
    };

    private static LibrarySearchResultDto Found(int artistTotal = 1, int itemTotal = 1, params ItemHitDto[] items) => LibraryData.EmptySearch() with
    {
        Query = "met",
        Tokens = ["met"],
        Artists = [ArtistHit()],
        Songs = [SongHit()],
        Items = items.Length > 0 ? items : [ItemHit()],
        Totals = new SearchTotalsDto { Artists = artistTotal, Albums = 0, Songs = 1, Items = itemTotal },
    };

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> TypeAsync(string text, string url = "library", string? folderId = null, ExperienceLevel level = ExperienceLevel.Advanced)
    {
        var page = RenderLibrary(url, level, folderId);
        await page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = text });
        Time.Advance(LiveSearchController.Debounce);
        page.WaitForAssertion(() => page.FindAll("[data-testid=search-results] .ss-skeleton").ShouldBeEmpty());
        return page;
    }

    [Fact]
    public async Task Results_ReplaceTheFolders_GroupedWithHeadingsAndCounts()
    {
        SearchAnswer = () => Found(artistTotal: 1, itemTotal: 1);

        var page = await TypeAsync("met");

        page.FindAll("[data-testid=library-folders]").ShouldBeEmpty();
        page.Find("[data-testid=search-results] h1").TextContent.ShouldBe("Search results");
        page.FindAll("[data-testid^=search-group-]").Select(g => g.GetAttribute("data-testid")).ShouldBe(["search-group-artists", "search-group-songs", "search-group-items"]);
        page.Find("[data-testid=search-group-artists] h2").TextContent.ShouldBe("Artists (1)");
        page.Find("[data-testid=search-group-items] h2").TextContent.ShouldBe("Items (1)");
        page.Find("[data-testid=search-group-artists]").GetAttribute("aria-labelledby").ShouldBe("search-heading-artists");
        page.FindAll("[data-testid^=search-show-all-]").ShouldBeEmpty();
        NavigatedTo.ShouldEndWith("library?q=met");
    }

    [Fact]
    public async Task FolderHits_AreLinksWithTheMatchMarked_AndTheirPathAsLinks()
    {
        SearchAnswer = () => Found();

        var page = await TypeAsync("met");

        var hit = page.Find($"[data-testid=search-hit-{LibraryData.MetallicaId}]");
        hit.GetAttribute("href").ShouldBe($"library/{LibraryData.MetallicaId}");
        var mark = hit.QuerySelector("[data-testid=search-mark]")!;
        (mark.TagName, mark.TextContent).ShouldBe(("MARK", "Met"));
        hit.TextContent.ShouldContain(", artist");
        var song = page.Find($"[data-testid=search-hit-{LibraryData.SongId}]").ParentElement!;
        song.QuerySelectorAll(".ss-search-path a").Select(a => a.GetAttribute("href")).ShouldBe([$"library/{LibraryData.MetallicaId}", $"library/{LibraryData.AlbumId}"]);
    }

    [Fact]
    public async Task ItemHits_AreThe007Rows_WithPathMarksAndMatchedInstruments()
    {
        SearchAnswer = () => Found();

        var page = await TypeAsync("met");

        var row = page.Find($"[data-testid=asset-row-{SnareId}]");
        row.QuerySelector($"[data-testid=asset-name-{SnareId}] [data-testid=search-mark]")!.TextContent.ShouldBe("snare");
        var path = page.Find($"[data-testid=asset-path-{SnareId}]");
        path.QuerySelectorAll("a").Length.ShouldBe(3);
        path.QuerySelector("[data-testid=search-mark]")!.TextContent.ShouldBe("Rick");
        row.QuerySelector(".ss-asset-matched [data-testid=search-mark]")!.TextContent.ShouldBe("Snare");
        row.QuerySelectorAll("[data-testid=search-mark]").Length.ShouldBe(3);
        page.Find($"[data-testid=asset-play-{SnareId}]").ShouldNotBeNull();
        page.Find($"[data-testid=asset-delete-{SnareId}]").ShouldNotBeNull();
    }

    [Fact]
    public async Task ItemAction_InTheResults_SearchesAgain()
    {
        SearchAnswer = () => Found();
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var page = await TypeAsync("met");

        await page.Find($"[data-testid=asset-delete-{SnareId}]").ClickAsync(new());

        await Api.Received(1).DeleteAssetAsync(SnareId, Arg.Any<CancellationToken>());
        await Api.Received(2).SearchLibraryAsync(Arg.Any<LibrarySearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MoreHitsThanShown_OffersShowAll_WhichAsksForTheLargestGroups()
    {
        SearchAnswer = () => Found(artistTotal: 45, itemTotal: 30);
        var page = await TypeAsync("met");

        page.Find("[data-testid=search-show-all-artists]").TextContent.ShouldBe("Show all 45 artists");
        page.Find("[data-testid=search-show-all-items]").TextContent.ShouldBe("Show all 30 items");
        await page.Find("[data-testid=search-show-all-artists]").ClickAsync(new());

        await Api.Received(1).SearchLibraryAsync(new LibrarySearchRequest("met", null, 200), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Groups_ShowAtMost20_UntilShowAll()
    {
        var many = Enumerable.Range(0, 25).Select(i => ItemHit(Guid.NewGuid(), $"Item {i}")).ToArray();
        SearchAnswer = () => Found(itemTotal: 25, items: many);
        var page = await TypeAsync("met");
        page.FindAll("[data-testid=search-group-items] [data-testid^=asset-row-]").Count.ShouldBe(20);

        await page.Find("[data-testid=search-show-all-items]").ClickAsync(new());

        page.WaitForAssertion(() => page.FindAll("[data-testid=search-group-items] [data-testid^=asset-row-]").Count.ShouldBe(25));
        page.FindAll("[data-testid=search-show-all-items]").ShouldBeEmpty();
    }

    [Fact]
    public async Task NoMatches_SaysSo_ListsWhatTheSearchLooksAt_AndClears()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());
        var page = await TypeAsync("zzz", $"library/{LibraryData.SongId}", LibraryData.SongId);

        var empty = page.Find("[data-testid=library-search-empty]");
        empty.TextContent.ShouldContain("No matches for 'zzz'.");
        empty.TextContent.ShouldContain("stems and instruments");
        await page.Find("[data-testid=library-search-empty-clear]").ClickAsync(new());

        page.FindAll("[data-testid=search-results]").ShouldBeEmpty();
        NavigatedTo.ShouldEndWith($"library/{LibraryData.SongId}");
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Failure_ShowsTheReason_AndKeepsThePreviousResults()
    {
        SearchAnswer = () => Found();
        var page = await TypeAsync("met");
        SearchAnswer = () => throw Problem(HttpStatusCode.InternalServerError, "The search failed.");

        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        page.Find("[data-testid=library-search-error]").TextContent.ShouldBe("The search failed.");
        page.Find($"[data-testid=search-hit-{LibraryData.MetallicaId}]").ShouldNotBeNull();
    }

    [Fact]
    public async Task ClearButton_ExistsWhileTheBoxHasText_AndRestoresTheFolderWithFocus()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());
        var page = RenderFolder(LibraryData.SongId);
        page.FindAll("[data-testid=library-search-clear]").ShouldBeEmpty();
        await page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = "bass" });

        var clear = page.Find("[data-testid=library-search-clear]");
        clear.GetAttribute("aria-label").ShouldBe("Clear search");
        await clear.ClickAsync(new());

        page.Find("[data-testid=library-folder-title]").TextContent.ShouldBe("Battery");
        page.FindAll("[data-testid=library-search-clear]").ShouldBeEmpty();
        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task Escape_ClearsTheSearch()
    {
        SearchAnswer = () => Found();
        var page = await TypeAsync("met");

        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.Find("[data-testid=library-folders]").ShouldNotBeNull();
        NavigatedTo.ShouldEndWith("library");
    }

    [Fact]
    public async Task Typing_ReplacesTheHistoryEntry_AndEnterPushesOne()
    {
        var page = RenderLibrary();

        await page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = "bass" });
        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Enter" });

        var history = Navigation.History.Take(2).Reverse().ToList();
        history.Select(h => (h.Uri, h.Options.ReplaceHistoryEntry)).ShouldBe([("library?q=bass", true), ("library?q=bass", false)]);
    }

    [Fact]
    public async Task ResultCount_IsAnnouncedPolitely_Once500MsAfterTheLastChange()
    {
        SearchAnswer = () => Found(artistTotal: 1, itemTotal: 1);
        var page = await TypeAsync("met");
        var status = page.Find("[data-testid=library-search-status]");
        (status.GetAttribute("role"), status.GetAttribute("aria-live")).ShouldBe(("status", "polite"));
        status.TextContent.ShouldBe(string.Empty);

        Time.Advance(LibraryAnnouncer.Pause);

        page.WaitForAssertion(() => page.Find("[data-testid=library-search-status]").TextContent.ShouldBe("3 results"));
        page.Find("[data-testid=library-search]").GetAttribute("aria-describedby").ShouldBe("library-search-status");
    }

    [Fact]
    public async Task ArrowDown_MovesFocusToTheFirstResult()
    {
        SearchAnswer = () => Found();
        var page = await TypeAsync("met");

        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task ArrowDown_WithOnlyItems_FocusesTheItemTable()
    {
        SearchAnswer = () => Found() with { Artists = [], Songs = [], Totals = new SearchTotalsDto { Artists = 0, Albums = 0, Songs = 0, Items = 1 } };
        var page = await TypeAsync("snare");

        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });

        JSInterop.VerifyFocusAsyncInvoke();
        page.Find("[data-testid=search-group-items] table").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public async Task ArrowDown_WithoutResults_DoesNothing()
    {
        var page = RenderLibrary();

        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "ArrowDown" });
        await page.Find("[data-testid=library-search]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "a" });

        JSInterop.Invocations.Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Scope_InsideAFolder_LimitsTheSearch()
    {
        Folder(LibraryData.AlbumId, LibraryData.View(LibraryData.Folder(LibraryData.AlbumId, "Master of Puppets")));
        var page = RenderFolder(LibraryData.AlbumId);
        await page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = "bass" });

        await page.Find("[data-testid=library-search-scope]").ChangeAsync(new ChangeEventArgs { Value = true });

        await Api.Received(1).SearchLibraryAsync(new LibrarySearchRequest("bass", LibraryData.AlbumId), Arg.Any<CancellationToken>());
        page.Find("[data-testid=library-search-scope]").HasAttribute("checked").ShouldBeTrue();
        await page.Find("[data-testid=library-search-scope]").ChangeAsync(new ChangeEventArgs { Value = false });
        await Api.Received(1).SearchLibraryAsync(new LibrarySearchRequest("bass"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void QueryInTheUrl_IsSearchedOnLoad()
    {
        SearchAnswer = () => Found();

        var page = RenderLibrary("library?q=met");

        page.Find("[data-testid=library-search]").GetAttribute("value").ShouldBe("met");
        page.Find($"[data-testid=search-hit-{LibraryData.MetallicaId}]").ShouldNotBeNull();
    }

    [Fact]
    public void OnlySpaces_KeepTheFolderView()
    {
        var page = RenderLibrary("library?q=%20%20");

        page.Find("[data-testid=library-folders]").ShouldNotBeNull();
        Api.DidNotReceive().SearchLibraryAsync(Arg.Any<LibrarySearchRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SlashShortcut_IsRegisteredForTheLibrary_AndFocusesTheSearch()
    {
        var page = RenderLibrary();

        Interop.HasShortcuts(ShortcutScope.Library).ShouldBeTrue();
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Library, LibraryPage.FocusSearchCommand));

        JSInterop.VerifyFocusAsyncInvoke();
    }

    [Fact]
    public async Task OtherCommands_AreIgnored_AndTheShortcutEndsWithThePage()
    {
        var page = RenderLibrary();

        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Library, "something.else"));
        await DisposeComponentsAsync();

        JSInterop.Invocations.Where(i => i.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase)).ShouldBeEmpty();
        Interop.HasShortcuts(ShortcutScope.Library).ShouldBeFalse();
    }

    [Fact]
    public async Task BackToTheFolderUrl_EndsTheSearch()
    {
        SearchAnswer = () => Found();
        var page = await TypeAsync("met");

        Navigation.NavigateTo("library");

        page.WaitForAssertion(() => page.Find("[data-testid=library-folders]").ShouldNotBeNull());
    }

    [Fact]
    public async Task DuplicateItemIds_FromTheApi_RenderOnce()
    {
        SearchAnswer = () => Found(itemTotal: 2, items: [ItemHit(), ItemHit()]);

        var page = await TypeAsync("met");

        page.FindAll($"[data-testid=asset-row-{SnareId}]").Count.ShouldBe(1);
    }
}
