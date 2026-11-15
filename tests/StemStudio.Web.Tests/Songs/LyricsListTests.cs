using System.Net;
using Bunit;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Components.Songs;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Songs;

/// <summary>The lyrics list and its editor (010 T036, US2-1/2, FR-003; ui-test-ids 1.9.0 lyrics-*).</summary>
public sealed class LyricsListTests : PageTestContext
{
    private static readonly Guid OtherId = Guid.Parse("5a0f2e1d-9c8b-4a7f-8e6d-c5b4a3f2e1d0");

    public LyricsListTests()
    {
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns([SongData.Sheet(), SongData.Sheet("Night bus", "Lights go by") with { Id = OtherId, Language = "nl" }]);
        Api.CreateLyricsSheetAsync(Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(ci => SongData.Sheet(ci.Arg<LyricsInput>().Title) with { Id = Guid.NewGuid() });
        Api.UpdateLyricsSheetAsync(Arg.Any<Guid>(), Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).Returns(ci => SongData.Sheet(ci.Arg<LyricsInput>().Title));
    }

    [Fact]
    public void List_ShowsEverySheet_WithItsActions()
    {
        var page = Render<LyricsListPage>();

        page.Find("h1").TextContent.ShouldBe("Lyrics");
        page.Find("[data-help-id=lyrics-library]").ShouldNotBeNull();
        var row = page.Find($"[data-testid=lyrics-row-{SongData.SheetId}]");
        row.TextContent.ShouldContain("Paper boats");
        row.TextContent.ShouldContain("English");
        row.TextContent.ShouldContain("Paper boats along the gutter");
        page.Find($"[data-testid=lyrics-song-{SongData.SheetId}]").GetAttribute("href").ShouldBe($"songs/new?lyricsId={SongData.SheetId}");
        page.FindAll($"[data-testid=lyrics-edit-{SongData.SheetId}]").Count.ShouldBe(1);
        page.FindAll($"[data-testid=lyrics-delete-{SongData.SheetId}]").Count.ShouldBe(1);
        page.Find($"[data-testid=lyrics-row-{OtherId}]").TextContent.ShouldContain("Dutch");
    }

    [Fact]
    public async Task Search_FiltersByTitleAndText_AndShowsTheEmptyState()
    {
        var page = Render<LyricsListPage>();

        await page.Find("[data-testid=lyrics-search]").InputAsync(new() { Value = "lights" });
        page.FindAll("[data-testid^=lyrics-row-]").Select(r => r.GetAttribute("data-testid")).ShouldBe([$"lyrics-row-{OtherId}"]);
        await page.Find("[data-testid=lyrics-search]").InputAsync(new() { Value = "nothing like this" });
        page.Find("[data-testid=lyrics-empty]").TextContent.ShouldContain("No lyrics match");
    }

    [Fact]
    public void NoSheets_ShowsTheEmptyState()
    {
        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns([]);

        var page = Render<LyricsListPage>();

        page.Find("[data-testid=lyrics-empty]").TextContent.ShouldContain("No lyrics yet");
    }

    [Fact]
    public async Task New_OpensTheEditor_AndSavesANewSheet()
    {
        var page = Render<LyricsListPage>();

        await page.Find("[data-testid=lyrics-new]").ClickAsync(new());
        page.Find("[role=dialog]").GetAttribute("data-help-id").ShouldBe("lyrics-format");
        await page.Find("[data-testid=lyrics-title-input]").InputAsync(new() { Value = "Rooftops" });
        await page.Find("[data-testid=lyrics-language-select]").ChangeAsync(new() { Value = "de" });
        await page.Find("[data-testid=lyrics-text-input]").InputAsync(new() { Value = "Over the rooftops" });
        await page.Find("[data-testid=lyrics-save]").ClickAsync(new());

        await Api.Received(1).CreateLyricsSheetAsync(Arg.Is<LyricsInput>(i => i.Title == "Rooftops" && i.Language == "de" && i.Text == "Over the rooftops"), Arg.Any<CancellationToken>());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
        page.FindAll("[data-testid^=lyrics-row-]").Count.ShouldBe(3);
    }

    [Fact]
    public async Task Edit_FillsTheEditor_AndReplacesTheRow()
    {
        var page = Render<LyricsListPage>();

        await page.Find($"[data-testid=lyrics-edit-{SongData.SheetId}]").ClickAsync(new());
        page.Find("[data-testid=lyrics-title-input]").GetAttribute("value").ShouldBe("Paper boats");
        page.Find("[data-testid=lyrics-text-input]").GetAttribute("value").ShouldBe(SongData.Lyrics);
        await page.Find("[data-testid=lyrics-title-input]").InputAsync(new() { Value = "Paper boats (v2)" });
        await page.Find("[data-testid=lyrics-save]").ClickAsync(new());

        await Api.Received(1).UpdateLyricsSheetAsync(SongData.SheetId, Arg.Is<LyricsInput>(i => i.Title == "Paper boats (v2)"), Arg.Any<CancellationToken>());
        page.Find($"[data-testid=lyrics-row-{SongData.SheetId}]").TextContent.ShouldContain("Paper boats (v2)");
    }

    [Fact]
    public async Task Editor_ShowsTheFieldProblem_AndCanBeCancelled()
    {
        Api.CreateLyricsSheetAsync(Arg.Any<LyricsInput>(), Arg.Any<CancellationToken>()).ThrowsAsync(new ApiException(new ApiError(
            HttpStatusCode.BadRequest, "One or more validation errors occurred.", null,
            new Dictionary<string, string[]> { ["text"] = ["The lyrics can be at most 4096 characters."] })));
        var page = Render<LyricsListPage>();

        await page.Find("[data-testid=lyrics-new]").ClickAsync(new());
        await page.Find("[data-testid=lyrics-save]").ClickAsync(new());
        page.Find("[data-testid=lyrics-error]").TextContent.ShouldBe("Enter a title of 1 to 80 characters.");
        await page.Find("[data-testid=lyrics-title-input]").InputAsync(new() { Value = "Too long" });
        await page.Find("[data-testid=lyrics-save]").ClickAsync(new());

        var error = page.Find("[data-testid=lyrics-error]");
        (error.GetAttribute("data-field"), error.TextContent).ShouldBe(("text", "The lyrics can be at most 4096 characters."));
        await page.Find("[data-testid=lyrics-cancel]").ClickAsync(new());
        page.FindAll("[role=dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Delete_AsksFirst_ThenRemovesTheRow()
    {
        var page = Render<LyricsListPage>();

        await page.Find($"[data-testid=lyrics-delete-{SongData.SheetId}]").ClickAsync(new());
        page.Find("[role=dialog]").TextContent.ShouldContain("keep their own copy");
        await page.Find("[data-testid=lyrics-delete-confirm]").ClickAsync(new());

        await Api.Received(1).DeleteLyricsSheetAsync(SongData.SheetId, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=lyrics-row-{SongData.SheetId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Failures_AreShown()
    {
        Api.DeleteLyricsSheetAsync(SongData.SheetId, Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.NotFound, "Lyrics not found"));
        var page = Render<LyricsListPage>();

        await page.Find($"[data-testid=lyrics-delete-{SongData.SheetId}]").ClickAsync(new());
        await page.Find("[data-testid=lyrics-delete-confirm]").ClickAsync(new());

        page.Find("[data-testid=lyrics-action-error]").TextContent.ShouldBe("Lyrics not found");

        Api.ListLyricsSheetsAsync(Arg.Any<string?>(), Arg.Any<CancellationToken>()).ThrowsAsync(Problem(HttpStatusCode.ServiceUnavailable, "The StemStudio API cannot be reached. Is it running?"));
        Render<LyricsListPage>().Find("[data-testid=lyrics-load-error]").TextContent.ShouldBe("The StemStudio API cannot be reached. Is it running?");
    }

    [Fact]
    public void Navigation_LinksToLyrics()
    {
        Render<StemStudio.Web.Components.Layout.NavMenu>().Find("[data-testid=nav-lyrics]").GetAttribute("href").ShouldBe("lyrics");
    }
}
