using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using NSubstitute;
using StemStudio.Contracts.Library;
using StemStudio.Web.Api;
using StemStudio.Web.Experience;
using StemStudio.Web.Library;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Library;

/// <summary>
/// The library info dialog for a track and for one item (022 T074, FR-031..FR-033; ui-test-ids 1.22.0 library-info-*):
/// automatic boxes with the automatic name and source, validation, reset, follow to the new folder, every level.
/// </summary>
public sealed class LibraryInfoDialogTests : LibraryTestContext
{
    private const string NewSongId = "so9999999999999999";

    private static readonly LibraryInfoRequest AceOfSpades = new() { Artist = null, Album = "Ace of Spades", Song = null };

    private static LibraryInfoStateDto State(string? album = null, PlacementDto? placement = null) => LibraryData.InfoState(album) with { Placement = placement };

    private static LibraryFolderViewDto SongWithItem(PlacementSourceDto songSource = PlacementSourceDto.Import)
    {
        var placement = LibraryData.Placement(LibraryData.SongId) with { Song = new PlacementLevelDto { FolderId = LibraryData.SongId, Name = "Battery", Source = songSource } };
        var item = LibraryData.Asset() with { Placement = placement, Provenance = AssetData.Provenance(LibraryData.BatteryTrackId) };
        return LibraryData.SongView() with { Items = [item] };
    }

    private async Task<IRenderedComponent<CascadingValue<ExperienceContext>>> OpenTrackDialogAsync(ExperienceLevel level = ExperienceLevel.Advanced,
        PlacementSourceDto songSource = PlacementSourceDto.Import)
    {
        Folder(LibraryData.SongId, SongWithItem(songSource));
        Api.GetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, Arg.Any<CancellationToken>()).Returns(State());
        var page = RenderFolder(LibraryData.SongId, level);
        await page.Find("[data-testid=library-info-edit]").ClickAsync(new());
        return page;
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    [InlineData(ExperienceLevel.Expert)]
    public async Task TrackDialog_IsAModal_AtEveryLevel_WithTheAutomaticNamesAndSources(ExperienceLevel level)
    {
        var page = await OpenTrackDialogAsync(level);

        var dialog = page.Find("[data-testid=library-info-dialog]");
        (dialog.GetAttribute("role"), dialog.GetAttribute("aria-modal"), dialog.GetAttribute("data-scope")).ShouldBe(("dialog", "true", "track"));
        dialog.GetAttribute("data-help-id").ShouldBe("library-info");
        page.Find("[data-testid=library-info-artist]").GetAttribute("value").ShouldBe(string.Empty);
        page.Find("[data-testid=library-info-artist]").GetAttribute("placeholder").ShouldBe("Rick Astley");
        page.Find("label[for=library-info-album]").TextContent.ShouldBe("Album");
        page.Find("[data-testid=library-info-auto-album]").HasAttribute("checked").ShouldBeTrue();
        page.Find("[data-testid=library-info-auto-album]").ParentElement!.TextContent.Trim().ShouldBe("Automatic: Unknown album (fallback)");
        page.Find("[data-testid=library-info-auto-artist]").ParentElement!.TextContent.Trim().ShouldBe("Automatic: Rick Astley (from the import)");
        page.Find("[data-testid=library-info-lookup]").GetAttribute("href").ShouldBe($"tracks/{LibraryData.BatteryTrackId}");
        page.Find("[data-testid=library-info-lookup]").TextContent.ShouldBe("Look up band info again");
    }

    [Fact]
    public async Task Save_SendsTheTypedName_AndFollowsTheItemsToTheirNewSongFolder()
    {
        var page = await OpenTrackDialogAsync();
        Api.SetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, AceOfSpades, Arg.Any<CancellationToken>()).Returns(State("Ace of Spades"));
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(State("Ace of Spades", LibraryData.Placement(NewSongId)));

        await page.Find("[data-testid=library-info-album]").InputAsync(new ChangeEventArgs { Value = "  Ace of Spades " });
        page.Find("[data-testid=library-info-auto-album]").HasAttribute("checked").ShouldBeFalse();
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        await Api.Received(1).SetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, AceOfSpades, Arg.Any<CancellationToken>());
        NavigatedTo.ShouldEndWith($"library/{NewSongId}");
        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task Save_WhenTheFolderStays_ReloadsIt()
    {
        var page = await OpenTrackDialogAsync();
        Api.SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>()).Returns(State());
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(State(placement: LibraryData.Placement(LibraryData.SongId)));

        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        FolderRequests(LibraryData.SongId).Count().ShouldBe(2);
        NavigatedTo.ShouldEndWith($"library/{LibraryData.SongId}");
    }

    [Fact]
    public async Task ItemsWithOwnNames_AreNotFollowed_AndTheFolderReloads()
    {
        var page = await OpenTrackDialogAsync(songSource: PlacementSourceDto.Item);
        Api.SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>()).Returns(State());

        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        await Api.DidNotReceive().GetAssetLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        FolderRequests(LibraryData.SongId).Count().ShouldBe(2);
    }

    [Fact]
    public async Task FollowLookupFailure_StillClosesAndReloads()
    {
        var page = await OpenTrackDialogAsync();
        Api.SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>()).Returns(State());
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns<LibraryInfoStateDto>(_ => throw Problem(HttpStatusCode.NotFound, "Gone"));

        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();
        FolderRequests(LibraryData.SongId).Count().ShouldBe(2);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("tab\tinside")]
    public async Task InvalidName_ShowsAReadableMessage_AndSendsNothing(string name)
    {
        var page = await OpenTrackDialogAsync();

        await page.Find("[data-testid=library-info-song]").InputAsync(new ChangeEventArgs { Value = name });
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        var input = page.Find("[data-testid=library-info-song]");
        input.GetAttribute("aria-invalid").ShouldBe("true");
        page.Find($"#{input.GetAttribute("aria-describedby")}").TextContent.ShouldBe(LibraryInfoForm.InvalidName);
        await Api.DidNotReceive().SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TooLongName_IsRefused()
    {
        var page = await OpenTrackDialogAsync();

        await page.Find("[data-testid=library-info-song]").InputAsync(new ChangeEventArgs { Value = new string('a', 201) });
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        page.Find("[data-testid=library-info-song]").GetAttribute("aria-invalid").ShouldBe("true");
        page.Find("[data-testid=library-info-song]").GetAttribute("maxlength").ShouldBe("200");
    }

    [Fact]
    public async Task ServerFieldMessage_IsShownAtTheFieldAndInTheError()
    {
        var page = await OpenTrackDialogAsync();
        Api.SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>()).Returns<LibraryInfoStateDto>(_ => throw new ApiException(
            new ApiError(HttpStatusCode.BadRequest, "One or more validation errors occurred.", null, new Dictionary<string, string[]> { ["album"] = ["Use 1 to 200 characters, or leave it automatic."] })));

        await page.Find("[data-testid=library-info-album]").InputAsync(new ChangeEventArgs { Value = "x" });
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        page.Find("[data-testid=library-info-error]").TextContent.ShouldBe("Use 1 to 200 characters, or leave it automatic.");
        page.Find("[data-testid=library-info-album]").GetAttribute("aria-invalid").ShouldBe("true");
        page.Find("[data-testid=library-info-dialog]").ShouldNotBeNull();
    }

    [Fact]
    public async Task TickingAutomatic_ClearsTheOwnName()
    {
        Folder(LibraryData.SongId, SongWithItem());
        Api.GetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, Arg.Any<CancellationToken>()).Returns(State("Ace of Spades"));
        Api.SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>()).Returns(State());
        var page = RenderFolder(LibraryData.SongId);
        await page.Find("[data-testid=library-info-edit]").ClickAsync(new());
        page.Find("[data-testid=library-info-album]").GetAttribute("value").ShouldBe("Ace of Spades");
        page.Find("[data-testid=library-info-auto-album]").HasAttribute("checked").ShouldBeFalse();

        await page.Find("[data-testid=library-info-auto-album]").ChangeAsync(new ChangeEventArgs { Value = true });
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        await Api.Received(1).SetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, LibraryInfoRequest.Automatic, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UntickingAutomatic_KeepsTheTypedText()
    {
        var page = await OpenTrackDialogAsync();

        await page.Find("[data-testid=library-info-auto-song]").ChangeAsync(new ChangeEventArgs { Value = false });

        page.Find("[data-testid=library-info-auto-song]").HasAttribute("checked").ShouldBeFalse();
    }

    [Fact]
    public async Task Reset_MakesEveryNameAutomatic()
    {
        var page = await OpenTrackDialogAsync();
        Api.ResetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, Arg.Any<CancellationToken>()).Returns(State());

        await page.Find("[data-testid=library-info-reset]").ClickAsync(new());

        await Api.Received(1).ResetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, Arg.Any<CancellationToken>());
        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelAndEscape_CloseWithoutSaving()
    {
        var page = await OpenTrackDialogAsync();
        await page.Find("[data-testid=library-info-cancel]").ClickAsync(new());
        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();

        await page.Find("[data-testid=library-info-edit]").ClickAsync(new());
        await page.Find("[data-testid=library-info-dialog]").KeyDownAsync(new Microsoft.AspNetCore.Components.Web.KeyboardEventArgs { Key = "Escape" });

        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();
        await Api.DidNotReceive().SetTrackLibraryInfoAsync(Arg.Any<Guid>(), Arg.Any<LibraryInfoRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadFailure_ShowsTheReason_AndSaveIsDisabled()
    {
        Folder(LibraryData.SongId, SongWithItem());
        Api.GetTrackLibraryInfoAsync(LibraryData.BatteryTrackId, Arg.Any<CancellationToken>()).Returns<LibraryInfoStateDto>(_ => throw Problem(HttpStatusCode.NotFound, "The track no longer exists."));
        var page = RenderFolder(LibraryData.SongId);

        await page.Find("[data-testid=library-info-edit]").ClickAsync(new());

        page.Find("[data-testid=library-info-error]").TextContent.ShouldBe("The track no longer exists.");
        page.Find("[data-testid=library-info-save]").HasAttribute("disabled").ShouldBeTrue();
    }

    [Theory]
    [InlineData(ExperienceLevel.Beginner)]
    [InlineData(ExperienceLevel.Advanced)]
    public async Task ItemDialog_SavesOnlyThatItem_AndTheFolderReloads(ExperienceLevel level)
    {
        Folder(LibraryData.SongId, SongWithItem());
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(State(placement: LibraryData.Placement(LibraryData.SongId)));
        var jazz = new LibraryInfoRequest { Artist = null, Album = null, Song = "Jazz take" };
        Api.SetAssetLibraryInfoAsync(AssetData.AssetId, jazz, Arg.Any<CancellationToken>()).Returns(State(placement: LibraryData.Placement(NewSongId)));
        var page = RenderFolder(LibraryData.SongId, level);

        var open = page.Find($"[data-testid=asset-library-info-{AssetData.AssetId}]");
        open.GetAttribute("aria-label").ShouldBe("Library info of Never Gonna Give You Up - bass");
        await open.ClickAsync(new());
        page.Find("[data-testid=library-info-dialog]").GetAttribute("data-scope").ShouldBe("item");
        page.FindAll("[data-testid=library-info-lookup]").ShouldBeEmpty();
        await page.Find("[data-testid=library-info-song]").InputAsync(new ChangeEventArgs { Value = "Jazz take" });
        await page.Find("[data-testid=library-info-save]").ClickAsync(new());

        await Api.Received(1).SetAssetLibraryInfoAsync(AssetData.AssetId, jazz, Arg.Any<CancellationToken>());
        FolderRequests(LibraryData.SongId).Count().ShouldBe(2);
    }

    [Fact]
    public async Task ItemReset_UsesTheItemOperation_AndARecordedTakeShowsTheConflict()
    {
        Folder(LibraryData.SongId, SongWithItem());
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(State());
        Api.ResetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns<LibraryInfoStateDto>(_ => throw Problem(HttpStatusCode.Conflict, "Recorded takes are not filed", "Recorded takes stay with their track."));
        var page = RenderFolder(LibraryData.SongId);
        await page.Find($"[data-testid=asset-library-info-{AssetData.AssetId}]").ClickAsync(new());

        await page.Find("[data-testid=library-info-reset]").ClickAsync(new());

        page.Find("[data-testid=library-info-error]").TextContent.ShouldBe("Recorded takes stay with their track.");
    }

    [Fact]
    public async Task ItemDialog_Cancel_ClosesIt()
    {
        Folder(LibraryData.SongId, SongWithItem());
        Api.GetAssetLibraryInfoAsync(AssetData.AssetId, Arg.Any<CancellationToken>()).Returns(State());
        var page = RenderFolder(LibraryData.SongId);
        await page.Find($"[data-testid=asset-library-info-{AssetData.AssetId}]").ClickAsync(new());

        await page.Find("[data-testid=library-info-cancel]").ClickAsync(new());

        page.FindAll("[data-testid=library-info-dialog]").ShouldBeEmpty();
    }

    [Fact]
    public void AllItems_RowsOfferLibraryInfoToo()
    {
        Api.ListAssetsAsync(Arg.Any<CancellationToken>()).Returns(AssetData.List(LibraryData.Asset()));

        var page = RenderLibrary("library?view=all");

        page.Find($"[data-testid=asset-library-info-{AssetData.AssetId}]").ShouldNotBeNull();
    }

    [Fact]
    public void EditButton_ExistsOnlyWhileTheTrackExists()
    {
        Folder(LibraryData.SongId, SongWithItem() with { Folder = LibraryData.SongView().Folder! with { TrackIds = [] } });

        var page = RenderFolder(LibraryData.SongId);

        page.FindAll("[data-testid=library-info-edit]").ShouldBeEmpty();
    }
}

/// <summary><see cref="LibraryInfoForm"/>: sources, validation and the request (022 T074, FR-031, FR-033).</summary>
public sealed class LibraryInfoFormTests
{
    [Theory]
    [InlineData(PlacementSourceDto.Match, "from band info")]
    [InlineData(PlacementSourceDto.Import, "from the import")]
    [InlineData(PlacementSourceDto.Track, "from the track's library info")]
    [InlineData(PlacementSourceDto.Item, "set for this item")]
    [InlineData(PlacementSourceDto.Fallback, "fallback")]
    public void SourceText_SaysWhereTheNameComesFrom(PlacementSourceDto source, string text) =>
        LibraryInfoForm.SourceText(source).ShouldBe(text);

    [Fact]
    public void Request_SendsNullForAutomatic_AndTrimmedNames()
    {
        var form = new LibraryInfoForm();
        form.Load(LibraryData.InfoState(" Ace of Spades "));

        form.Validate().ShouldBeTrue();
        form.Request().ShouldBe(new LibraryInfoRequest { Artist = null, Album = "Ace of Spades", Song = null });
    }

    [Fact]
    public void ServerErrors_ForOtherFields_AreNotShownAtAField()
    {
        var form = new LibraryInfoForm();

        form.ShowErrors(new Dictionary<string, string[]> { ["q"] = ["x"], ["song"] = [] }).ShouldBeFalse();
        form.Fields.ShouldAllBe(f => f.Problem == null);
    }
}
