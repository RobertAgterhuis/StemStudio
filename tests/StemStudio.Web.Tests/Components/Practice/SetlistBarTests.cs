using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Setlists;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Layout;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Components.Practice;
using StemStudio.Web.Interop;
using StemStudio.Web.Practice;
using StemStudio.Web.Tests.Fakes;
using StemStudio.Web.Tests.Mixer;

namespace StemStudio.Web.Tests.Components.Practice;

/// <summary>T092: setlist play mode (FR-017): the bar, the entry's mixer with its mix, next/previous by button and key, exit.</summary>
public sealed class SetlistBarTests : MixerTestContext
{
    private static readonly Guid Id = PracticeFlowData.SetlistId;
    private static readonly SetlistDto Setlist = PracticeFlowData.Setlist([PracticeFlowData.Entry(1, note: "Start slow"), PracticeFlowData.Entry(2, mixId: TestData.MixId)]);

    public SetlistBarTests()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(Setlist);
        Api.GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()).Returns(TestData.Mix());
    }

    private IRenderedComponent<SetlistPlay> RenderPlay(int position) =>
        Render<SetlistPlay>(p => p.Add(c => c.SetlistId, Id).Add(c => c.Position, position));

    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public void TheBar_ShowsThePositionAndTheNote_AndTheMixerOfTheEntry()
    {
        var page = RenderPlay(1);

        var bar = page.Find("[data-testid=setlist-bar]");
        (bar.GetAttribute("data-setlist-id"), bar.GetAttribute("data-position"), bar.GetAttribute("data-count")).ShouldBe((Id.ToString(), "1", "2"));
        page.Find("[data-testid=setlist-bar-note]").TextContent.ShouldBe("Start slow");
        page.Find("[data-testid=setlist-bar-previous]").HasAttribute("disabled").ShouldBeTrue();
        page.WaitForAssertion(() => page.Find("[data-testid=transport-play]").ShouldNotBeNull());
    }

    [Fact]
    public void TheEntryMix_IsLoaded()
    {
        var page = RenderPlay(2);

        page.WaitForAssertion(() => Api.Received(1).GetMixAsync(TestData.MixId, Arg.Any<CancellationToken>()));
        page.FindAll("[data-testid=setlist-bar-note]").ShouldBeEmpty();
        page.Find("[data-testid=setlist-bar-next]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task NextPreviousAndExit_Navigate()
    {
        var page = RenderPlay(1);

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-bar-next]").Click());
        CurrentUri.ShouldEndWith($"setlists/{Id}/play/2");
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-bar-exit]").Click());
        CurrentUri.ShouldEndWith($"setlists/{Id}");
    }

    [Fact]
    public async Task SetlistKeys_MoveThroughTheSetlist_AndStopAtTheEnds()
    {
        var page = RenderPlay(1);
        page.WaitForAssertion(() => Interop.HasShortcuts(ShortcutScope.Setlist).ShouldBeTrue());
        Interop.KeyMaps[ShortcutScope.Setlist].Bindings.Select(b => b.Command).ShouldBe(["setlist.next", "setlist.previous"]);

        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Setlist, "setlist.previous"));
        CurrentUri.ShouldNotContain("/play/0");
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Setlist, "setlist.next"));
        CurrentUri.ShouldEndWith($"setlists/{Id}/play/2");
        await page.InvokeAsync(() => Interop.CommandAsync(ShortcutScope.Setlist, "other"));

        await page.Instance.DisposeAsync();
        Interop.HasShortcuts(ShortcutScope.Setlist).ShouldBeFalse();
    }

    [Fact]
    public void APositionOutsideTheSetlist_OrAnError_IsShown()
    {
        RenderPlay(5).Find("[data-testid=setlist-save-error]").TextContent.ShouldContain("no song at that position");

        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>())
            .ThrowsAsync(new ApiException(new ApiError(HttpStatusCode.NotFound, "Gone", "Setlist not found.", new Dictionary<string, string[]>())));
        RenderPlay(1).Find("[data-testid=setlist-save-error]").TextContent.ShouldContain("Setlist not found.");
    }
}

/// <summary>T092: "Add to setlist" on a track and a mix, and the rail link.</summary>
public sealed class AddToSetlistTests : PageTestContext
{
    private static readonly Guid Id = PracticeFlowData.SetlistId;

    [Fact]
    public async Task AddsTheTrackWithItsMix_AtTheEnd()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([PracticeFlowData.SetlistSummary()]);
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());
        var button = Render<AddToSetlist>(p => p.Add(c => c.TrackId, TestData.TrackId).Add(c => c.MixId, TestData.MixId));

        await button.InvokeAsync(() => button.Find($"[data-testid=add-to-setlist-mix-{TestData.MixId}]").Click());
        await button.InvokeAsync(() => button.Find("button:not([data-testid])").Click());

        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r =>
            r.Entries.Count == 3 && r.Entries[2].TrackId == TestData.TrackId && r.Entries[2].MixId == TestData.MixId), Arg.Any<CancellationToken>());
        Services.GetRequiredService<StemStudio.Web.Services.ToastService>().Message.ShouldBe("Added to Friday rehearsal");
    }

    [Fact]
    public async Task WithoutSetlists_LinksToTheSetlistsPage_AndAFullSetlistIsRefused()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([], [PracticeFlowData.SetlistSummary()]);
        var full = PracticeFlowData.Setlist([.. Enumerable.Range(1, SetlistEntries.MaxEntries).Select(i => PracticeFlowData.Entry(1) with { Id = Guid.NewGuid(), Position = i })]);
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(full);
        var button = Render<AddToSetlist>(p => p.Add(c => c.TrackId, TestData.TrackId));

        await button.InvokeAsync(() => button.Find($"[data-testid=add-to-setlist-{TestData.TrackId}]").Click());
        button.Find("a").GetAttribute("href").ShouldBe("setlists");
        await button.InvokeAsync(() => button.Find($"[data-testid=add-to-setlist-{TestData.TrackId}]").Click());
        await button.InvokeAsync(() => button.Find($"[data-testid=add-to-setlist-{TestData.TrackId}]").Click());
        await button.InvokeAsync(() => button.Find("button:not([data-testid])").Click());

        Services.GetRequiredService<StemStudio.Web.Services.ToastService>().Message.ShouldBe("Friday rehearsal is full (100 songs).");
        await Api.DidNotReceive().ReplaceSetlistEntriesAsync(Arg.Any<Guid>(), Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnApiError_IsAToast()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([PracticeFlowData.SetlistSummary()]);
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException("Setlist not found."));
        var button = Render<AddToSetlist>(p => p.Add(c => c.TrackId, TestData.TrackId));

        await button.InvokeAsync(() => button.Find($"[data-testid=add-to-setlist-{TestData.TrackId}]").Click());
        await button.InvokeAsync(() => button.Find("button:not([data-testid])").Click());

        Services.GetRequiredService<StemStudio.Web.Services.ToastService>().Message.ShouldBe("Setlist not found.");
    }

    [Fact]
    public void TheRail_HasTheSetlistsLink()
    {
        var shell = Render<MainLayout>(p => p.Add(l => l.Body, (RenderFragment)(b => b.AddMarkupContent(0, "<h1>Page</h1>"))));

        shell.Find("[data-testid=nav-setlists]").GetAttribute("href").ShouldBe("setlists");
    }

    [Fact]
    public void Entries_MoveClampsAndIgnoresNoOps()
    {
        var entries = PracticeFlowData.Setlist().Entries;

        SetlistEntries.Move(entries, 0, 0).ShouldBeNull();
        SetlistEntries.Move(entries, 5, 0).ShouldBeNull();
        SetlistEntries.Move(entries, 0, 9)![1].Id.ShouldBe(entries[0].Id);
        SetlistEntries.PlayRoute(Id, 3).ShouldBe($"setlists/{Id}/play/3");
        Should.Throw<ArgumentNullException>(() => SetlistEntries.Inputs(null!));
        Should.Throw<ArgumentNullException>(() => SetlistEntries.Change(entries, Guid.Empty, null!));
        _ = new MixSummaryDto(Guid.Empty, Guid.Empty, "t", "n", DateTimeOffset.UnixEpoch);
    }
}
