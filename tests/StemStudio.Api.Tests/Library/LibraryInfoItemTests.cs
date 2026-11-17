using System.Net;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using static StemStudio.Api.Tests.Library.LibraryInfoApi;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T072: library info of one item - GET/PUT/DELETE, placement, moved items, 409 <c>not-fileable</c> (FR-031..FR-033).</summary>
public sealed class LibraryInfoItemTests : ApiTestBase
{
    [Fact]
    public async Task Get_ReturnsTheStateWithThePlacement()
    {
        var item = await FiledItemAsync();

        using var response = await Client.GetItemInfoAsync(item.Id);

        await ContractAssert.MatchesAsync(response, "getAssetLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        Of(state.Artist).ShouldBe((null, "Rick Astley", PlacementSourceDto.Import, "Rick Astley"));
        (state.Placement!.Song.FolderId, state.Placement.Filed, state.MovedItems).ShouldBe(("soc02cef10277ea444", true, 0));
    }

    [Fact]
    public async Task Get_AutomaticOfAnItem_IsTheTrackName()
    {
        var item = await FiledItemAsync();
        await Client.PutTrackInfoAsync(item.TrackId!.Value, Names(album: "Ace of Spades"));

        var state = await (await Client.GetItemInfoAsync(item.Id)).BodyAsync<LibraryInfoStateDto>();

        Of(state.Album).ShouldBe((null, "Ace of Spades", PlacementSourceDto.Track, "Ace of Spades"));
    }

    [Fact]
    public async Task Put_SetsTheItemName_AndRefilesTheItem()
    {
        var item = await FiledItemAsync();

        using var response = await Client.PutItemInfoAsync(item.Id, Names(song: "Ace of Spades (jazz take)"));

        await ContractAssert.MatchesAsync(response, "setAssetLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        Of(state.Song).ShouldBe(("Ace of Spades (jazz take)", "Ace of Spades (jazz take)", PlacementSourceDto.Item, "Never Gonna Give You Up"));
        (state.Placement!.Song.Name, state.Placement.Song.Source, state.MovedItems).ShouldBe(("Ace of Spades (jazz take)", PlacementSourceDto.Item, 1));
        Level((await Factory.FindAssetAsync(item.Id))!.Placement!.Song).ShouldBe(("Ace of Spades (jazz take)", PlacementSource.Item));
    }

    [Fact]
    public async Task Put_ItemLevelsAreKept_WhenTheTrackChanges()
    {
        var item = await FiledItemAsync();
        await Client.PutItemInfoAsync(item.Id, Names(album: "Live"));

        var state = await (await Client.PutTrackInfoAsync(item.TrackId!.Value, Names(album: "Studio", song: "Renamed"))).BodyAsync<LibraryInfoStateDto>();

        state.MovedItems.ShouldBe(1);
        var placement = (await Factory.FindAssetAsync(item.Id))!.Placement!;
        (Level(placement.Album), Level(placement.Song)).ShouldBe((("Live", PlacementSource.Item), ("Renamed", PlacementSource.Track)));
    }

    [Fact]
    public async Task Delete_UsesTheTrackOrAutomaticNamesAgain()
    {
        var item = await FiledItemAsync();
        await Client.PutItemInfoAsync(item.Id, Names("X", "Y", "Z"));

        using var response = await Client.DeleteItemInfoAsync(item.Id);

        await ContractAssert.MatchesAsync(response, "resetAssetLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        (state.Artist.Value, state.Song.Effective, state.Song.Source, state.MovedItems).ShouldBe((null, "Never Gonna Give You Up", PlacementSourceDto.Import, 1));
    }

    [Fact]
    public async Task Put_InvalidName_Is400_AndChangesNothing()
    {
        var item = await FiledItemAsync();

        using var response = await Client.PutItemInfoAsync(item.Id, Names(artist: "ok", album: new string('a', 201)));

        await ContractAssert.MatchesAsync(response, "setAssetLibraryInfo");
        (await response.FieldErrorsAsync("album")).ShouldBe([LibraryInfoRules.Message]);
        (await Factory.FindAssetAsync(item.Id))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Fact]
    public async Task ItemWithoutTrack_KeepsItsPlace_AndCanBeRenamed()
    {
        var item = Asset.Create(Guid.NewGuid(), "a", ExportProvenance(Guid.NewGuid()), T0);
        await Factory.FileAsync(item);

        var state = await (await Client.PutItemInfoAsync(item.Id, Names(artist: "Somebody"))).BodyAsync<LibraryInfoStateDto>();

        Of(state.Artist).ShouldBe(("Somebody", "Somebody", PlacementSourceDto.Item, "Rick Astley"));
        state.Song.Effective.ShouldBe("Never Gonna Give You Up");
    }

    [Fact]
    public async Task NotFileableItem_Is409OnWrites_AndHasNoPlacement()
    {
        var item = Asset.Create(Guid.NewGuid(), "take", ExportProvenance(null) with { Origin = (AssetOrigin)99 }, T0);
        await Factory.AddUnfiledAsync(item);

        using var put = await Client.PutItemInfoAsync(item.Id, Names(song: "x"));
        using var delete = await Client.DeleteItemInfoAsync(item.Id);
        using var get = await Client.GetItemInfoAsync(item.Id);

        await ContractAssert.MatchesAsync(put, "setAssetLibraryInfo");
        await ContractAssert.MatchesAsync(delete, "resetAssetLibraryInfo");
        new[] { put.StatusCode, delete.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.Conflict);
        (await put.JsonAsync()).GetProperty("type").GetString().ShouldBe(LibraryProblemTypes.Uri(LibraryProblemTypes.NotFileable));
        (await get.BodyAsync<LibraryInfoStateDto>()).Placement.ShouldBeNull();
        (await Factory.FindAssetAsync(item.Id))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Fact]
    public async Task UnknownItem_Is404()
    {
        var id = Guid.NewGuid();

        using var get = await Client.GetItemInfoAsync(id);
        using var put = await Client.PutItemInfoAsync(id, Names(album: "x"));
        using var delete = await Client.DeleteItemInfoAsync(id);

        await ContractAssert.MatchesAsync(get, "getAssetLibraryInfo");
        await ContractAssert.MatchesAsync(put, "setAssetLibraryInfo");
        await ContractAssert.MatchesAsync(delete, "resetAssetLibraryInfo");
        new[] { get.StatusCode, put.StatusCode, delete.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
    }

    private async Task<Asset> FiledItemAsync()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var item = Asset.Create(Guid.NewGuid(), "drums", ExportProvenance(track.Id), T0);
        await Factory.FileAsync(item);
        return item;
    }
}
