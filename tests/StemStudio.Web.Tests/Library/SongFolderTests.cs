using System.Net;
using Bunit;
using NSubstitute;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.Library;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>Song folders: the 007 item rows and actions, paths and the track links (022 T050, FR-020).</summary>
public sealed class SongFolderTests : LibraryTestContext
{
    private static readonly Guid SnareId = Guid.Parse("7d4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");
    private static readonly Guid MixId = Guid.Parse("8e4b2c8e-6a1f-4b3d-9e5c-2a3b4c5d6e7f");

    private static LibraryFolderViewDto Song(bool trackExists = true, params AssetDto[] items)
    {
        var view = LibraryData.SongView();
        var folder = view.Folder! with { TrackIds = trackExists ? view.Folder!.TrackIds : [] };
        return view with { Folder = folder, Items = items.Length > 0 ? items : [LibraryData.Asset(), LibraryData.Asset(SnareId, name: "Battery - snare"), LibraryData.Asset(MixId, name: "What I hear")] };
    }

    [Fact]
    public void Items_AreThe007Rows_InTheOrderOfTheApi_WithTheirPathAndAnchor()
    {
        Folder(LibraryData.SongId, Song());

        var page = RenderFolder(LibraryData.SongId);

        page.FindAll("[data-testid^=asset-row-]").Select(r => r.GetAttribute("data-testid")).ShouldBe(
            [$"asset-row-{AssetData.AssetId}", $"asset-row-{SnareId}", $"asset-row-{MixId}"]);
        page.Find($"[data-testid=asset-row-{SnareId}]").Id.ShouldBe($"asset-{SnareId}");
        page.Find($"[data-testid=asset-path-{AssetData.AssetId}]").TextContent.ShouldBe("Rick Astley > Unknown album > Never Gonna Give You Up");
        foreach (var action in new[] { "play", "download", "rename", "delete", "score" })
        {
            page.FindAll($"[data-testid=asset-{action}-{SnareId}]").Count.ShouldBe(1, action);
        }

        page.Find("table caption").TextContent.ShouldBe("Items of Battery");
    }

    [Fact]
    public void TrackLinks_ExistOnlyWhileTheTrackExists()
    {
        Folder(LibraryData.SongId, Song());
        var page = RenderFolder(LibraryData.SongId);

        page.Find("[data-testid=song-open-track]").GetAttribute("href").ShouldBe($"tracks/{LibraryData.BatteryTrackId}");
        page.Find("[data-testid=song-open-mixer]").GetAttribute("href").ShouldBe($"tracks/{LibraryData.BatteryTrackId}/mixer");
    }

    [Fact]
    public void TrackLinks_AreGone_WhenTheTrackWasDeleted()
    {
        Folder(LibraryData.SongId, Song(trackExists: false));

        var page = RenderFolder(LibraryData.SongId);

        page.FindAll("[data-testid=song-open-track], [data-testid=song-open-mixer]").ShouldBeEmpty();
    }

    [Fact]
    public void SongFolder_HasNoLayoutOrSortControls()
    {
        Folder(LibraryData.SongId, Song());

        var page = RenderFolder(LibraryData.SongId);

        page.FindAll("[data-testid=library-layout-grid], [data-testid=library-sort]").ShouldBeEmpty();
        page.Find("[data-testid=library-view-all]").ShouldNotBeNull();
    }

    [Fact]
    public void EmptySongFolder_SaysSo()
    {
        Folder(LibraryData.SongId, LibraryData.SongView());

        var page = RenderFolder(LibraryData.SongId);

        page.Find("[data-testid=library-empty]").TextContent.ShouldContain("No items in this song folder");
    }

    [Fact]
    public async Task Rename_ReplacesTheRow()
    {
        Folder(LibraryData.SongId, Song());
        Api.RenameAssetAsync(SnareId, "Snare for practice", Arg.Any<CancellationToken>()).Returns(LibraryData.Asset(SnareId, name: "Snare for practice"));
        var page = RenderFolder(LibraryData.SongId);

        await page.Find($"[data-testid=asset-rename-{SnareId}]").ClickAsync(new());
        await page.Find("[data-testid=asset-rename-input]").InputAsync(new() { Value = "Snare for practice" });
        await page.Find("[data-testid=asset-rename-confirm]").ClickAsync(new());

        page.Find($"[data-testid=asset-name-{SnareId}]").TextContent.ShouldBe("Snare for practice");
    }

    [Fact]
    public async Task Delete_AsksFirst_AndReloadsTheFolder()
    {
        Folder(LibraryData.SongId, () => Song(), () => Song(true, LibraryData.Asset(), LibraryData.Asset(MixId, name: "What I hear")));
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var page = RenderFolder(LibraryData.SongId);

        await page.Find($"[data-testid=asset-delete-{SnareId}]").ClickAsync(new());

        await Api.Received(1).DeleteAssetAsync(SnareId, Arg.Any<CancellationToken>());
        page.FindAll($"[data-testid=asset-row-{SnareId}]").ShouldBeEmpty();
    }

    [Fact]
    public async Task LastItemDeleted_ShowsTheFolderIsEmptyNow()
    {
        Folder(LibraryData.SongId, () => Song(true, LibraryData.Asset()), () => throw Problem(HttpStatusCode.NotFound, "This folder is empty now"));
        Confirm.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        var page = RenderFolder(LibraryData.SongId);

        await page.Find($"[data-testid=asset-delete-{AssetData.AssetId}]").ClickAsync(new());

        page.Find("[data-testid=library-folder-missing]").ShouldNotBeNull();
    }

    [Fact]
    public async Task Retry_ReplacesTheRow_AndPollsUntilReady()
    {
        Folder(LibraryData.SongId, () => Song(true, LibraryData.Asset(status: AssetStatusDto.Failed)), () => Song(true, LibraryData.Asset()));
        Api.RetryAssetAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(LibraryData.Asset(status: AssetStatusDto.Queued));
        var page = RenderFolder(LibraryData.SongId);

        await page.Find($"[data-testid=asset-retry-{AssetData.AssetId}]").ClickAsync(new());
        page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("data-status").ShouldBe("queued");
        Time.Advance(StemStudio.Web.Components.Library.FolderView.PollEvery);
        Timer.Tick();

        page.WaitForAssertion(() => page.Find($"[data-testid=asset-row-{AssetData.AssetId}]").GetAttribute("data-status").ShouldBe("ready"));
    }

    [Fact]
    public async Task RetryRefused_ShowsTheReason()
    {
        Folder(LibraryData.SongId, Song(true, LibraryData.Asset(status: AssetStatusDto.Failed)));
        Api.RetryAssetAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns<AssetDto>(_ => throw Problem(HttpStatusCode.Conflict, "Busy"));
        var page = RenderFolder(LibraryData.SongId);

        await page.Find($"[data-testid=asset-retry-{AssetData.AssetId}]").ClickAsync(new());

        page.Find("[data-testid=library-action-error]").TextContent.ShouldBe("Busy");
    }

    [Fact]
    public void Beginner_HasTheItemActions()
    {
        Folder(LibraryData.SongId, Song());

        var page = RenderFolder(LibraryData.SongId, ExperienceLevel.Beginner);

        page.FindAll("[data-testid^=asset-rename-]").Count.ShouldBe(3);
        page.FindAll("[data-testid^=asset-delete-]").Count.ShouldBe(3);
        page.Find("[data-testid=song-open-track]").ShouldNotBeNull();
    }

    [Fact]
    public void ItemLink_FromAConfirmation_TargetsTheRowAnchor() =>
        LibraryLinks.Item(SnareId, LibraryData.Placement(LibraryData.SongId)).ShouldBe($"library/{LibraryData.SongId}#asset-{SnareId}");
}
