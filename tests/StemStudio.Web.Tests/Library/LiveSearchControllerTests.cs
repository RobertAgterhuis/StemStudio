using System.Net;
using Microsoft.Extensions.Time.Testing;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// <see cref="LiveSearchController"/> with a fake clock (022 T064, FR-022, FR-026, R-07): first character, 200 ms debounce,
/// Enter at once, stale answers dropped, cancellation, Escape/clear back to the folder, <c>?q=</c> replace/push.
/// </summary>
public sealed class LiveSearchControllerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 17, 10, 0, 0, TimeSpan.Zero));
    private readonly List<(LibrarySearchRequest Request, TaskCompletionSource<LibrarySearchResultDto> Answer, CancellationToken Token)> _searches = [];
    private readonly List<(string Url, bool Replace)> _navigations = [];
    private readonly LiveSearchController _search;
    private int _focus;
    private int _changes;

    public LiveSearchControllerTests()
    {
        _search = new LiveSearchController(new LiveSearchPorts(Search, (url, replace) => _navigations.Add((url, replace)), Focus, work => work()), _time);
        _search.Changed += () => _changes++;
    }

    public void Dispose() => _search.Dispose();

    private static LibrarySearchResultDto Result(string query, int items = 0) =>
        LibraryData.EmptySearch() with { Query = query, Totals = new SearchTotalsDto { Artists = 0, Albums = 0, Songs = 0, Items = items } };

    private Task<LibrarySearchResultDto> Search(LibrarySearchRequest request, CancellationToken token)
    {
        var answer = new TaskCompletionSource<LibrarySearchResultDto>();
        _searches.Add((request, answer, token));
        return answer.Task;
    }

    private ValueTask Focus()
    {
        _focus++;
        return ValueTask.CompletedTask;
    }

    private Task Start(string url = $"http://host/library/{LibraryData.SongId}") => _search.StartAsync(LibraryLocation.Parse(url));

    [Fact]
    public async Task FirstCharacter_SearchesAfter200Ms_NotBefore()
    {
        await Start();

        _search.Input("s");
        _time.Advance(TimeSpan.FromMilliseconds(199));
        _searches.ShouldBeEmpty();
        _search.Busy.ShouldBeTrue();
        _search.IsActive.ShouldBeTrue();

        _time.Advance(TimeSpan.FromMilliseconds(1));
        _searches.ShouldHaveSingleItem().Request.ShouldBe(new LibrarySearchRequest("s"));
        _searches[0].Answer.SetResult(Result("s", 3));

        await Task.Yield();
        _search.Result!.Totals.Items.ShouldBe(3);
        _search.Busy.ShouldBeFalse();
    }

    [Fact]
    public async Task Typing_RestartsTheDebounce_AndOnlyTheLatestTextIsSearched()
    {
        await Start();

        _search.Input("s");
        _time.Advance(TimeSpan.FromMilliseconds(150));
        _search.Input("sn");
        _time.Advance(TimeSpan.FromMilliseconds(150));
        _search.Input("sna");
        _time.Advance(TimeSpan.FromMilliseconds(200));

        _searches.ShouldHaveSingleItem().Request.Query.ShouldBe("sna");
    }

    [Fact]
    public async Task Typing_ReplacesTheUrl_AndEnterPushesIt_FromTheOpenFolder()
    {
        await Start();

        _search.Input("lead vocals");
        var submit = _search.SubmitAsync();

        _navigations.ShouldBe([($"library/{LibraryData.SongId}?q=lead%20vocals", true), ($"library/{LibraryData.SongId}?q=lead%20vocals", false)]);
        _searches.ShouldHaveSingleItem().Request.Query.ShouldBe("lead vocals");
        _time.Advance(TimeSpan.FromSeconds(1));
        _searches.Count.ShouldBe(1);
        _searches[0].Answer.SetResult(Result("lead vocals"));
        await submit;
    }

    [Fact]
    public async Task StaleAnswers_AreDropped_AndTheOlderRequestIsCancelled()
    {
        await Start();
        _search.Input("m");
        _time.Advance(LiveSearchController.Debounce);
        _search.Input("me");
        _time.Advance(LiveSearchController.Debounce);

        _searches.Count.ShouldBe(2);
        _searches[0].Token.IsCancellationRequested.ShouldBeTrue();
        _searches[1].Answer.SetResult(Result("me", 2));
        _searches[0].Answer.SetResult(Result("m", 9));
        await Task.Yield();

        _search.Result!.Query.ShouldBe("me");
    }

    [Fact]
    public async Task CancelledRequest_ChangesNothing()
    {
        await Start();
        _search.Input("m");
        _time.Advance(LiveSearchController.Debounce);
        _search.Input("me");
        _time.Advance(LiveSearchController.Debounce);
        var changes = _changes;

        _searches[0].Answer.SetCanceled(TestContext.Current.CancellationToken);
        await Task.Yield();

        _changes.ShouldBe(changes);
        _search.Busy.ShouldBeTrue();
    }

    [Fact]
    public async Task Failure_ShowsTheReason_AndKeepsThePreviousResults()
    {
        await Start();
        _search.Input("bass");
        _time.Advance(LiveSearchController.Debounce);
        _searches[0].Answer.SetResult(Result("bass", 4));
        _search.Input("bass!");
        _time.Advance(LiveSearchController.Debounce);

        _searches[1].Answer.SetException(new ApiException(new ApiError(HttpStatusCode.UnprocessableEntity, "Invalid", null,
            new Dictionary<string, string[]> { ["q"] = ["Type at least one letter or digit."] })));
        await Task.Yield();

        _search.Error.ShouldBe("Type at least one letter or digit.");
        _search.Result!.Query.ShouldBe("bass");
    }

    [Fact]
    public async Task StaleFailure_IsIgnored()
    {
        await Start();
        _search.Input("a");
        _time.Advance(LiveSearchController.Debounce);
        _search.Input("ab");
        _time.Advance(LiveSearchController.Debounce);

        _searches[0].Answer.SetException(new ApiException(ApiError.Unreachable));
        await Task.Yield();

        _search.Error.ShouldBeNull();
    }

    [Fact]
    public async Task Clear_RestoresTheFolderUrl_AndFocusesTheBox()
    {
        await Start();
        _search.Input("snare");
        _time.Advance(LiveSearchController.Debounce);
        _searches[0].Answer.SetResult(Result("snare"));

        await _search.ClearAsync();

        (_search.Text, _search.Result, _search.IsActive).ShouldBe((string.Empty, null, false));
        _navigations[^1].ShouldBe(($"library/{LibraryData.SongId}", false));
        _focus.ShouldBe(1);
    }

    [Fact]
    public async Task Clear_WithoutText_OnlyFocuses()
    {
        await Start();

        await _search.ClearAsync();

        _navigations.ShouldBeEmpty();
        _focus.ShouldBe(1);
    }

    [Fact]
    public async Task BlankText_ShowsTheFolderAgain_WithoutASearch()
    {
        await Start();
        _search.Input("s");

        _search.Input("   ");
        _time.Advance(TimeSpan.FromSeconds(1));

        _searches.ShouldBeEmpty();
        _search.IsActive.ShouldBeFalse();
        _navigations[^1].ShouldBe(($"library/{LibraryData.SongId}", true));
        await _search.SubmitAsync();
        _searches.ShouldBeEmpty();
    }

    [Fact]
    public async Task LongText_IsCutAt100Characters()
    {
        await Start();

        _search.Input(new string('x', 150));

        _search.Text.Length.ShouldBe(100);
    }

    [Fact]
    public async Task UrlWithQuery_SearchesAtOnce_AndTheSameTextAgainDoesNothing()
    {
        var start = Start($"http://host/library?q=motorhead");
        _searches.ShouldHaveSingleItem().Request.Query.ShouldBe("motorhead");
        _searches[0].Answer.SetResult(Result("motorhead"));
        await start;

        await Start($"http://host/library?q=motorhead");

        _searches.Count.ShouldBe(1);
        _search.Text.ShouldBe("motorhead");
    }

    [Fact]
    public async Task BackToAUrlWithoutQuery_EndsTheSearch()
    {
        var start = Start($"http://host/library?q=bass");
        _searches[0].Answer.SetResult(Result("bass"));
        await start;

        await Start($"http://host/library/{LibraryData.AlbumId}");

        (_search.IsActive, _search.Result).ShouldBe((false, null));
    }

    [Fact]
    public async Task Scope_LimitsToTheFolder_AndIsDroppedInAnotherFolder()
    {
        await Start();
        _search.Input("bass");
        var scoped = _search.ScopeAsync(LibraryData.SongId);

        _searches.ShouldHaveSingleItem().Request.ShouldBe(new LibrarySearchRequest("bass", LibraryData.SongId));
        _searches[0].Answer.SetResult(Result("bass"));
        await scoped;
        _search.Within.ShouldBe(LibraryData.SongId);

        await Start($"http://host/library/{LibraryData.AlbumId}");
        _search.Within.ShouldBeNull();
    }

    [Fact]
    public async Task ShowAll_AsksForTheLargestGroups_UntilTheTextChanges()
    {
        await Start();
        _search.Input("drum");
        var all = _search.ShowAllAsync();

        _searches.ShouldHaveSingleItem().Request.Limit.ShouldBe(200);
        _searches[0].Answer.SetResult(Result("drum"));
        await all;
        _search.Input("drums");
        _time.Advance(LiveSearchController.Debounce);

        _searches[1].Request.Limit.ShouldBeNull();
    }

    [Fact]
    public async Task Refresh_WithoutText_DoesNothing()
    {
        await Start();

        await _search.RefreshAsync();
        await _search.ScopeAsync(null);

        _searches.ShouldBeEmpty();
    }

    [Fact]
    public async Task Dispose_StopsThePendingDebounce()
    {
        await Start();
        _search.Input("s");

        _search.Dispose();
        _time.Advance(TimeSpan.FromSeconds(1));

        _searches.ShouldBeEmpty();
    }
}

/// <summary><see cref="LibraryAnnouncer"/>: the result count, once, 500 ms after the last change (022 T066, FR-027).</summary>
public sealed class LibraryAnnouncerTests : IDisposable
{
    private readonly FakeTimeProvider _time = new();
    private readonly LibraryAnnouncer _announcer;
    private int _changes;

    public LibraryAnnouncerTests()
    {
        _announcer = new LibraryAnnouncer(_time, work => work());
        _announcer.Changed += () => _changes++;
    }

    public void Dispose() => _announcer.Dispose();

    private static LibrarySearchResultDto Result(int artists, int items) =>
        LibraryData.EmptySearch() with { Totals = new SearchTotalsDto { Artists = artists, Albums = 0, Songs = 0, Items = items } };

    [Theory]
    [InlineData(0, 0, "No results")]
    [InlineData(1, 0, "1 result")]
    [InlineData(2, 10, "12 results")]
    [InlineData(0, 1200, "1,200 results")]
    public void CountText_IsReadable(int artists, int items, string text) =>
        LibraryAnnouncer.CountText(Result(artists, items)).ShouldBe(text);

    [Fact]
    public void Announces_Once_500MsAfterTheLastChange()
    {
        _announcer.Schedule(Result(0, 3));
        _time.Advance(TimeSpan.FromMilliseconds(400));
        _announcer.Schedule(Result(0, 12));
        _time.Advance(TimeSpan.FromMilliseconds(499));
        _announcer.Message.ShouldBe(string.Empty);

        _time.Advance(TimeSpan.FromMilliseconds(1));

        _announcer.Message.ShouldBe("12 results");
        _changes.ShouldBe(1);
    }

    [Fact]
    public void TheSameCount_IsNotAnnouncedAgain()
    {
        _announcer.Schedule(Result(0, 3));
        _time.Advance(LibraryAnnouncer.Pause);
        _announcer.Schedule(Result(3, 0));
        _time.Advance(LibraryAnnouncer.Pause);

        _changes.ShouldBe(1);
    }

    [Fact]
    public void ClearedSearch_EmptiesTheRegionAtOnce()
    {
        _announcer.Schedule(Result(0, 3));
        _time.Advance(LibraryAnnouncer.Pause);

        _announcer.Schedule(null);

        _announcer.Message.ShouldBe(string.Empty);
        _changes.ShouldBe(2);
    }
}
