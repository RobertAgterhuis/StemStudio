using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// The live search box keeps what the user typed while the URL changes it asked for come back late (022 E2E: fast typing
/// lost characters, "qam" became "qm", "snare" became "snre"). Blazor Server reports every <c>?q=</c> navigation back as
/// a location change one round trip later and the router then sets the page's parameters again; those echoes may come
/// late, out of order, twice or not at all. Only a genuine navigation (Back, Forward, a link) puts a URL text in the box.
/// </summary>
public sealed class LibrarySearchEchoTests : LibraryTestContext
{
    private readonly DeferredNavigationManager _navigation = new();
    private IRenderedComponent<CascadingValue<ExperienceContext>> _page = default!;

    public LibrarySearchEchoTests() => Services.AddSingleton<NavigationManager>(_navigation);

    private string BoxValue => _page.Find("[data-testid=library-search]").GetAttribute("value") ?? string.Empty;

    private bool HasClearButton => _page.FindAll("[data-testid=library-search-clear]").Count > 0;

    private void Open() => _page = RenderAtLevel<LibraryPage>(ExperienceLevel.Advanced, p => p.Add(x => x.FolderId, null));

    private Task TypeAsync(string text) => _page.Find("[data-testid=library-search]").InputAsync(new ChangeEventArgs { Value = text });

    private Task KeyAsync(string key) => _page.Find("[data-testid=library-search]").KeyDownAsync(new KeyboardEventArgs { Key = key });

    // The browser carried out navigation i: the location change arrives, then the router sets the page's parameters again.
    private async Task EchoAsync(int index, bool routerFirst = false)
    {
        if (routerFirst)
        {
            await RouterRefreshAsync();
        }

        await _page.InvokeAsync(() => _navigation.Echo(index));
        if (!routerFirst)
        {
            await RouterRefreshAsync();
        }
    }

    private async Task GoAsync(string uri, string? state = null)
    {
        await _page.InvokeAsync(() => _navigation.Go(uri, state));
        await RouterRefreshAsync();
    }

    private Task RouterRefreshAsync() =>
        _page.InvokeAsync(() => _page.FindComponent<LibraryPage>().Render(p => p.Add(x => x.FolderId, null)));

    private void ShouldShow(string text)
    {
        BoxValue.ShouldBe(text);
        HasClearButton.ShouldBe(text.Length > 0);
    }

    private IEnumerable<string> SearchedTexts() =>
        Api.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(IStemStudioApiClient.SearchLibraryAsync))
            .Select(c => ((LibrarySearchRequest)c.GetArguments()[0]!).Query);

    [Fact]
    public async Task EchoesInOrder_ArrivingAfterFasterTyping_KeepTheTypedText()
    {
        Open();
        await TypeAsync("q");
        await TypeAsync("qa");
        await TypeAsync("qam");

        for (var i = 0; i < 3; i++)
        {
            await EchoAsync(i);
            ShouldShow("qam");
        }
    }

    [Fact]
    public async Task EchoesOutOfOrder_KeepTheTypedText()
    {
        Open();
        await TypeAsync("q");
        await TypeAsync("qa");
        await TypeAsync("qam");

        foreach (var i in new[] { 1, 0, 2 })
        {
            await EchoAsync(i, routerFirst: i == 0);
            ShouldShow("qam");
        }
    }

    [Fact]
    public async Task DuplicatedEchoes_KeepTheTypedText()
    {
        Open();
        await TypeAsync("b");
        await TypeAsync("ba");

        await EchoAsync(0);
        await EchoAsync(0);
        await RouterRefreshAsync();
        ShouldShow("ba");

        await TypeAsync("bas");
        await EchoAsync(1, routerFirst: true);
        await EchoAsync(1);
        ShouldShow("bas");
    }

    [Fact]
    public async Task CoalescedEchoes_OnlyTheLastOneArrives_KeepTheTypedText()
    {
        Open();
        await TypeAsync("s");
        await TypeAsync("sn");
        await TypeAsync("sna");

        await EchoAsync(2);
        ShouldShow("sna");

        await TypeAsync("snar");
        await EchoAsync(0);
        ShouldShow("snar");
    }

    [Fact]
    public async Task EchoesArrivingBetweenFurtherKeystrokes_KeepEveryCharacter_AndTheLastTextIsSearched()
    {
        Open();
        await TypeAsync("s");
        await TypeAsync("sn");
        await EchoAsync(0);
        await TypeAsync("sna");
        await EchoAsync(1);
        await TypeAsync("snar");
        await TypeAsync("snare");
        await EchoAsync(2);
        ShouldShow("snare");
        await EchoAsync(3);
        await EchoAsync(4);

        Time.Advance(LiveSearchController.Debounce);

        _page.WaitForAssertion(() => SearchedTexts().ShouldBe(["snare"]));
        ShouldShow("snare");
    }

    [Fact]
    public async Task Back_ToAnEntryTheSearchPushed_RestoresItsText()
    {
        Open();
        await TypeAsync("bass");
        await KeyAsync("Enter");
        await EchoAsync(0);
        await EchoAsync(1);
        var pushed = _navigation.Requests[1];

        await KeyAsync("Escape");
        await EchoAsync(2);
        ShouldShow(string.Empty);

        await GoAsync(pushed.Uri, pushed.Options.HistoryEntryState);

        ShouldShow("bass");
        _page.FindAll("[data-testid=search-results]").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ALinkWithAnotherText_WhileEchoesArePending_Wins_AndLateEchoesDoNotUndoIt()
    {
        Open();
        await TypeAsync("a");
        await TypeAsync("ab");

        await GoAsync("library?q=drum");
        ShouldShow("drum");

        await EchoAsync(0);
        await EchoAsync(1);
        ShouldShow("drum");
    }

    [Fact]
    public async Task InitialLoad_TakesTheTextFromTheUrl()
    {
        _navigation.Go("library?q=metal");

        Open();

        ShouldShow("metal");
    }
}
