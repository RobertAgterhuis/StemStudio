using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Setlists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Pages;
using StemStudio.Web.Experience;
using StemStudio.Web.Preferences;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Components.Pages;

/// <summary>T092: setlists page and setlist editor (FR-015, FR-016, FR-018; ui-test-ids 1.13.0 setlist*).</summary>
public sealed class SetlistsTests : PageTestContext
{
    private static readonly Guid Id = PracticeFlowData.SetlistId;

    private static ApiException Problem(string message) =>
        new(new ApiError(HttpStatusCode.UnprocessableEntity, "Problem", message, new Dictionary<string, string[]>()));

    private IRenderedComponent<CascadingValue<ExperienceContext>> RenderEditor(ExperienceLevel level = ExperienceLevel.Advanced) =>
        Render<CascadingValue<ExperienceContext>>(p => p
            .Add(c => c.Value, new ExperienceContext(level, DensityPreference.Comfortable))
            .AddChildContent<SetlistEditor>(m => m.Add(x => x.SetlistId, Id)));

    private string CurrentUri => Services.GetRequiredService<NavigationManager>().Uri;

    [Fact]
    public void List_ShowsTheSetlistsWithTheirActions()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([PracticeFlowData.SetlistSummary()]);

        var page = Render<Setlists>();

        page.Find($"[data-testid=setlist-row-{Id}]").TextContent.ShouldContain("7:46");
        page.Find($"[data-testid=setlist-open-{Id}]").GetAttribute("href").ShouldBe($"setlists/{Id}");
        page.Find("[data-testid=setlists-page]").ShouldNotBeNull();
    }

    [Fact]
    public void Empty_SaysHowToStart()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([]);

        Render<Setlists>().Find("[data-testid=setlists-empty]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Create_OpensTheEditorOfTheNewSetlist()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([]);
        Api.CreateSetlistAsync(Arg.Any<SetlistCreateRequest>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());
        var page = Render<Setlists>();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-create]").Click());
        page.Find("[data-testid=setlist-create-confirm]").HasAttribute("disabled").ShouldBeTrue();
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-create-name]").Input("  Gig  "));
        await page.InvokeAsync(() => page.Find("form").Submit());

        await Api.Received(1).CreateSetlistAsync(Arg.Is<SetlistCreateRequest>(r => r.Name == "Gig"), Arg.Any<CancellationToken>());
        CurrentUri.ShouldEndWith($"setlists/{Id}");
    }

    [Fact]
    public async Task PlayAndDelete()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).Returns([PracticeFlowData.SetlistSummary()], (IReadOnlyList<SetlistSummaryDto>)[]);
        var page = Render<Setlists>();

        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-play-{Id}]").Click());
        CurrentUri.ShouldEndWith($"setlists/{Id}/play/1");
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-delete-{Id}]").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-delete-confirm]").Click());

        await Api.Received(1).DeleteSetlistAsync(Id, Arg.Any<CancellationToken>());
        page.WaitForAssertion(() => page.Find("[data-testid=setlists-empty]").ShouldNotBeNull());
    }

    [Fact]
    public async Task ListAndCreateErrors_AreShown()
    {
        Api.ListSetlistsAsync(Arg.Any<CancellationToken>()).ThrowsAsync(Problem("The API is down."));
        Api.CreateSetlistAsync(Arg.Any<SetlistCreateRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem("Name too long."));
        Api.DeleteSetlistAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem("Gone."));
        var page = Render<Setlists>();
        page.Find("[data-testid=setlists-error]").TextContent.ShouldContain("The API is down.");

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-create]").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-create-name]").Input("x"));
        await page.InvokeAsync(() => page.Find("form").Submit());

        page.Find("[data-testid=setlists-error]").TextContent.ShouldContain("Name too long.");
    }

    [Fact]
    public void Editor_ShowsNameTotalEntriesAndTheRemovedNote()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist(removed: 1));

        var page = RenderEditor();

        page.Find("[data-testid=setlist-name]").GetAttribute("value").ShouldBe("Friday rehearsal");
        page.Find("[data-testid=setlist-total]").TextContent.ShouldBe("2 songs, 7:46");
        var first = PracticeFlowData.Setlist().Entries[0];
        page.Find($"[data-testid=setlist-entry-{first.Id}]").GetAttribute("data-position").ShouldBe("0");
        page.Find($"[data-testid=setlist-entry-{first.Id}] .ss-setlist-title").TextContent.ShouldStartWith("1. Song 1");
        page.Find($"[data-testid=setlist-entry-note-{first.Id}]").GetAttribute("value").ShouldBe("Start slow");
        page.Find("[data-testid=setlist-removed-note]").TextContent.ShouldContain("1 song was removed");
        page.Find($"[data-testid=setlist-entry-up-{first.Id}]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public void Editor_AtBeginner_HasNoNotesOrMixes_AndDownloadsThePdfDirectly()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());

        var page = RenderEditor(ExperienceLevel.Beginner);

        page.FindAll("[data-testid=setlist-note]").ShouldBeEmpty();
        page.FindAll("[data-testid^=setlist-entry-note-]").ShouldBeEmpty();
        page.FindAll("[data-testid^=setlist-entry-mix-]").ShouldBeEmpty();
        var pdf = page.Find("[data-testid=setlist-pdf]");
        pdf.TagName.ShouldBe("A");
        pdf.GetAttribute("href").ShouldEndWith($"/api/setlists/{Id}/pdf?lyrics=true&sections=true&notes=true&pageBreakPerSong=true");
    }

    [Fact]
    public async Task Editor_MovesByButtonsAndDrag_AndRemoves()
    {
        var setlist = PracticeFlowData.Setlist();
        var (first, second) = (setlist.Entries[0], setlist.Entries[1]);
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(setlist);
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-down-{first.Id}]").Click());
        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Id == second.Id && r.Entries[1].Id == first.Id),
            Arg.Any<CancellationToken>());
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-{second.Id}]").DragStart());
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-{first.Id}]").Drop());
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-{first.Id}]").Drop());
        await Api.Received(2).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Id == second.Id), Arg.Any<CancellationToken>());
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-remove-{first.Id}]").Click());

        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries.Count == 1 && r.Entries[0].Id == second.Id),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("down", 0, "down")]
    [InlineData("up", 1, "down")]
    [InlineData("down", 1, "up")]
    public async Task Editor_AfterAMove_TheFocusFollowsTheMovedEntry(string button, int moved, string focused)
    {
        // Keyboard reorder (US5 scenario 2): Enter on Move down keeps the focus on the moved song; when the button it used
        // is disabled at its new place (first or last), the other move button of that song gets the focus.
        var setlist = PracticeFlowData.Setlist([PracticeFlowData.Entry(1), PracticeFlowData.Entry(2), PracticeFlowData.Entry(3)]);
        var entry = setlist.Entries[moved];
        var order = setlist.Entries.ToList();
        order.RemoveAt(moved);
        order.Insert(button == "down" ? moved + 1 : moved - 1, entry);
        var after = setlist with { Entries = [.. order.Select((e, i) => e with { Position = i })] };
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(after);
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-{button}-{entry.Id}]").Click());

        var target = page.FindComponents<Radzen.Blazor.RadzenButton>()
            .Single(b => b.Instance.Attributes?["data-testid"] as string == $"setlist-entry-{focused}-{entry.Id}");
        page.WaitForAssertion(() => ((ElementReference)JSInterop.VerifyFocusAsyncInvoke().Arguments[0]!).Id.ShouldBe(target.Instance.Element.Id));
    }

    [Fact]
    public async Task Editor_AMoveWhileTheNoteIsSaving_WaitsForIt_AndKeepsTheNote()
    {
        // E2E setlists scenarios 1-2: the note (Tab) and Move down (Enter) came in quick succession; the move was built from
        // the entries before the note was stored and its save wiped the note.
        var setlist = PracticeFlowData.Setlist();
        var (first, second) = (setlist.Entries[0], setlist.Entries[1]);
        var noted = setlist with { Entries = [first with { Note = "Start soft." }, second] };
        var gate = new TaskCompletionSource<SetlistDto>();
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Id == first.Id), Arg.Any<CancellationToken>())
            .Returns(gate.Task);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Id == second.Id), Arg.Any<CancellationToken>())
            .Returns(noted with { Entries = [second, noted.Entries[0]] });
        var page = RenderEditor();

        var note = page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-note-{first.Id}]").Change("Start soft."));
        var move = page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-down-{first.Id}]").Click());
        gate.SetResult(noted);
        await Task.WhenAll(note, move);

        await Api.Received(1).ReplaceSetlistEntriesAsync(Id,
            Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Id == second.Id && r.Entries[1].Note == "Start soft."), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Editor_RenamesNotesAndMixes()
    {
        var setlist = PracticeFlowData.Setlist();
        var first = setlist.Entries[0];
        Api.GetSetlistAsync(Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(setlist);
        Api.UpdateSetlistAsync(Id, Arg.Any<SetlistUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ListMixesAsync(first.TrackId, Arg.Any<CancellationToken>()).Returns([new Contracts.Mixes.MixSummaryDto(TestData.MixId, first.TrackId, "Song 1", "Slow", DateTimeOffset.UnixEpoch)]);
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-name]").Change("Saturday gig"));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-name]").Change("  "));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-note]").Change("Bring spare strings"));
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-note-{first.Id}]").Change("Capo 2"));
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-mix-{first.Id}]").Focus());
        page.FindAll($"[data-testid=setlist-entry-mix-{first.Id}] option").Select(o => o.TextContent).ShouldBe(["Default mix", "Slow"]);
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-mix-{first.Id}]").Change(TestData.MixId.ToString()));

        await Api.Received(1).UpdateSetlistAsync(Id, Arg.Is<SetlistUpdateRequest>(r => r.Name == "Saturday gig"), Arg.Any<CancellationToken>());
        await Api.Received(1).UpdateSetlistAsync(Id, Arg.Is<SetlistUpdateRequest>(r => r.Note == "Bring spare strings"), Arg.Any<CancellationToken>());
        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].Note == "Capo 2"), Arg.Any<CancellationToken>());
        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r => r.Entries[0].MixId == TestData.MixId), Arg.Any<CancellationToken>());
        Api.ReceivedCalls().Count(c => c.GetMethodInfo().Name == "UpdateSetlistAsync").ShouldBe(2);
    }

    [Fact]
    public async Task Editor_AddsASongWithAMix()
    {
        var setlist = PracticeFlowData.Setlist();
        var other = Guid.NewGuid();
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(setlist);
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([TestData.Track(), TestData.Track(TrackStatusDto.Failed) with { Id = other }]);
        Api.ListMixesAsync(TestData.TrackId, Arg.Any<CancellationToken>()).Returns([new Contracts.Mixes.MixSummaryDto(TestData.MixId, TestData.TrackId, "x", "Live", DateTimeOffset.UnixEpoch)]);
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>()).Returns(setlist);
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-add]").Click());
        page.FindAll($"[data-testid=setlist-add-track-{other}]").ShouldBeEmpty();
        page.Find("[data-testid=setlist-add-confirm]").HasAttribute("disabled").ShouldBeTrue();
        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-add-track-{TestData.TrackId}]").Change(true));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-add-mix]").Change(TestData.MixId.ToString()));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-add-confirm]").Click());

        await Api.Received(1).ReplaceSetlistEntriesAsync(Id, Arg.Is<SetlistEntriesUpdateRequest>(r =>
            r.Entries.Count == 3 && r.Entries[2].TrackId == TestData.TrackId && r.Entries[2].MixId == TestData.MixId && r.Entries[2].Id == null), Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=setlist-add-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Editor_AddDialog_WithoutFinishedSongs_AndEscapeCloses()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());
        Api.ListTracksAsync(Arg.Any<CancellationToken>()).Returns([]);
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-add]").Click());
        page.Find("[data-testid=setlist-add-dialog]").TextContent.ShouldContain("No finished songs");
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-add-dialog]").KeyDown("Escape"));

        page.FindAll("[data-testid=setlist-add-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Editor_SaveError_IsShown_AndTheStoredSetlistReloaded()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist());
        Api.ReplaceSetlistEntriesAsync(Id, Arg.Any<SetlistEntriesUpdateRequest>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem("The mix belongs to another track."));
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find($"[data-testid=setlist-entry-remove-{PracticeFlowData.Setlist().Entries[0].Id}]").Click());

        page.Find("[data-testid=setlist-save-error]").TextContent.ShouldBe("The mix belongs to another track.");
        await Api.Received(2).GetSetlistAsync(Id, false, Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Editor_LoadError_IsShown()
    {
        Api.GetSetlistAsync(Id, false, Arg.Any<CancellationToken>()).ThrowsAsync(Problem("Setlist not found."));

        RenderEditor().Find("[data-testid=setlist-save-error]").TextContent.ShouldContain("Setlist not found.");
    }

    [Fact]
    public async Task Editor_PdfDialog_OptionsBuildTheDownloadLink_AndAnEmptySetlistSaysWhy()
    {
        Api.GetSetlistAsync(Id, Arg.Any<bool>(), Arg.Any<CancellationToken>()).Returns(PracticeFlowData.Setlist(removed: 1), PracticeFlowData.Setlist(entries: []));
        var page = RenderEditor();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf]").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf-lyrics]").Change(false));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf-page-break]").Change(false));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf-sections]").Change(true));
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf-notes]").Change(true));
        page.Find("[data-testid=setlist-pdf-download]").GetAttribute("href").ShouldEndWith("pdf?lyrics=false&sections=true&notes=true&pageBreakPerSong=false");
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf-dialog]").KeyDown("Escape"));
        page.FindAll("[data-testid=setlist-pdf-dialog]").ShouldBeEmpty();

        await page.InvokeAsync(() => page.Find("[data-testid=setlist-removed-note] button").Click());
        await page.InvokeAsync(() => page.Find("[data-testid=setlist-pdf]").Click());
        page.Find("[data-testid=setlist-pdf-error]").ShouldNotBeNull();
        page.FindAll("[data-testid=setlist-pdf-download]").ShouldBeEmpty();
        await Api.Received(1).GetSetlistAsync(Id, true, Arg.Any<CancellationToken>());
    }
}
