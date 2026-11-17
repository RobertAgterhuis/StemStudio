using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Library;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Library;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Library;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;
using static StemStudio.Api.Tests.Library.LibraryInfoApi;
using static StemStudio.Api.Tests.Library.LibraryTestData;

namespace StemStudio.Api.Tests.Library;

/// <summary>022 T072: library info of a track - GET/PUT/DELETE, sources, automatic names, moved items (FR-031..FR-033, FR-009).</summary>
public sealed class LibraryInfoTrackTests : ApiTestBase
{
    [Fact]
    public async Task Get_ShowsAutomaticNamesAndTheirSources()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.GetTrackInfoAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getTrackLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        Of(state.Artist).ShouldBe((null, "Rick Astley", PlacementSourceDto.Import, "Rick Astley"));
        Of(state.Album).ShouldBe((null, "Unknown album", PlacementSourceDto.Fallback, "Unknown album"));
        Of(state.Song).ShouldBe((null, "Never Gonna Give You Up", PlacementSourceDto.Import, "Never Gonna Give You Up"));
        (state.Placement, state.MovedItems).ShouldBe((null, 0));
    }

    [Fact]
    public async Task Get_OfAMatchedTrack_ShowsBandInfoAsSource()
    {
        var track = await Factory.SeedAsync(MatchedTrack());

        var state = await (await Client.GetTrackInfoAsync(track.Id)).BodyAsync<LibraryInfoStateDto>();

        Of(state.Album).ShouldBe((null, "Metallica", PlacementSourceDto.Match, "Metallica"));
    }

    [Fact]
    public async Task Put_RefilesItemsWithoutOwnNames_AndKeepsItemNames()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var (automatic, own) = Items(track.Id);
        own.SetLibraryInfo(new LibraryInfo { Album = "Mine" }, T0);
        await Factory.FileAsync(automatic, own);

        using var response = await Client.PutTrackInfoAsync(track.Id, Names(album: "  Ace of Spades  "));

        await ContractAssert.MatchesAsync(response, "setTrackLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        Of(state.Album).ShouldBe(("Ace of Spades", "Ace of Spades", PlacementSourceDto.Track, "Unknown album"));
        state.MovedItems.ShouldBe(1);
        Level((await Factory.FindAssetAsync(automatic.Id))!.Placement!.Album).ShouldBe(("Ace of Spades", PlacementSource.Track));
        Level((await Factory.FindAssetAsync(own.Id))!.Placement!.Album).ShouldBe(("Mine", PlacementSource.Item));
        (await Factory.FindTrackAsync(track.Id))!.LibraryInfo.ShouldBe(new LibraryInfo { Album = "Ace of Spades" });
    }

    [Fact]
    public async Task LaterLookup_NeverOverridesUserNames()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var (item, _) = Items(track.Id);
        await Factory.FileAsync(item);
        (await Client.PutTrackInfoAsync(track.Id, Names(artist: "My Band"))).StatusCode.ShouldBe(HttpStatusCode.OK);

        await RefileAfterLookupAsync(track.Id, Found("Metallica", "Metallica", "Enter Sandman"));

        var placement = (await Factory.FindAssetAsync(item.Id))!.Placement!;
        Level(placement.Artist).ShouldBe(("My Band", PlacementSource.Track));
        Level(placement.Album).ShouldBe(("Metallica", PlacementSource.Match));
        var state = await (await Client.GetTrackInfoAsync(track.Id)).BodyAsync<LibraryInfoStateDto>();
        Of(state.Artist).ShouldBe(("My Band", "My Band", PlacementSourceDto.Track, "Metallica"));
    }

    [Fact]
    public async Task Delete_MakesEveryLevelAutomaticAgain()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var (item, _) = Items(track.Id);
        await Factory.FileAsync(item);
        await Client.PutTrackInfoAsync(track.Id, Names("A", "B", "C"));

        using var response = await Client.DeleteTrackInfoAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "resetTrackLibraryInfo");
        var state = await response.BodyAsync<LibraryInfoStateDto>();
        (state.Artist.Value, state.Album.Value, state.Song.Value, state.MovedItems).ShouldBe((null, null, null, 1));
        Level((await Factory.FindAssetAsync(item.Id))!.Placement!.Artist).ShouldBe(("Rick Astley", PlacementSource.Import));
    }

    [Fact]
    public async Task Put_SameNamesTwice_MovesNothingTheSecondTime()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var (item, _) = Items(track.Id);
        await Factory.FileAsync(item);
        await Client.PutTrackInfoAsync(track.Id, Names(song: "Other"));

        var state = await (await Client.PutTrackInfoAsync(track.Id, Names(song: "Other"))).BodyAsync<LibraryInfoStateDto>();

        state.MovedItems.ShouldBe(0);
    }

    [Theory]
    [InlineData("artist", "   ")]
    [InlineData("album", "")]
    [InlineData("song", "bell\u0007")]
    [InlineData("album", "tab\tinside")]
    public async Task Put_InvalidName_Is400WithTheField(string field, string value)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        using var response = await Client.PutTrackInfoAsync(track.Id, With(field, value));

        await ContractAssert.MatchesAsync(response, "setTrackLibraryInfo");
        (await response.FieldErrorsAsync(field)).ShouldBe([LibraryInfoRules.Message]);
        (await Factory.FindTrackAsync(track.Id))!.LibraryInfo.ShouldBe(LibraryInfo.Automatic);
    }

    [Fact]
    public async Task Put_NameLongerThanTheContractLimit_Is400_ButExactlyTheLimitIsAccepted()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var limit = new string('x', ContractLimits.LibraryNameMaxLength);

        using var tooLong = await Client.PutTrackInfoAsync(track.Id, Names(song: limit + "x"));
        using var exact = await Client.PutTrackInfoAsync(track.Id, Names(song: limit));

        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooLong.FieldErrorsAsync("song")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(exact, "setTrackLibraryInfo");
        (await exact.BodyAsync<LibraryInfoStateDto>()).Song.Effective.ShouldBe(limit);
    }

    [Theory]
    [InlineData("""{"artist":null,"album":null}""")]
    [InlineData("""{"artist":1,"album":null,"song":null}""")]
    [InlineData("not json")]
    public async Task Put_MalformedBody_Is400(string body)
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await Client.PutAsync(new Uri(ApiRoutes.TrackLibraryInfo(track.Id), UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task UnknownTrack_Is404()
    {
        var id = Guid.NewGuid();

        using var get = await Client.GetTrackInfoAsync(id);
        using var put = await Client.PutTrackInfoAsync(id, Names(album: "x"));
        using var delete = await Client.DeleteTrackInfoAsync(id);

        await ContractAssert.MatchesAsync(get, "getTrackLibraryInfo");
        await ContractAssert.MatchesAsync(put, "setTrackLibraryInfo");
        await ContractAssert.MatchesAsync(delete, "resetTrackLibraryInfo");
        new[] { get.StatusCode, put.StatusCode, delete.StatusCode }.ShouldAllBe(status => status == HttpStatusCode.NotFound);
    }

    private static (Asset Automatic, Asset Own) Items(Guid trackId) =>
        (Asset.Create(Guid.NewGuid(), "a", ExportProvenance(trackId), T0), Asset.Create(Guid.NewGuid(), "b", ExportProvenance(trackId, channel: "bass"), T0));

    private static LibraryInfoRequest With(string field, string value) => field switch
    {
        "artist" => Names(artist: value),
        "album" => Names(album: value),
        _ => Names(song: value),
    };

    private Task<int> RefileAfterLookupAsync(Guid trackId, LookupResult result) => Factory.WithScopeAsync(async sp =>
    {
        var tracks = sp.GetRequiredService<ITrackRepository>();
        var track = (await tracks.FindAsync(trackId, Ct))!;
        Match(track, result);
        var moved = await sp.GetRequiredService<ILibraryFiler>().RefileTrackAsync(track, Ct);
        await tracks.SaveAsync(Ct);
        return moved;
    });
}
