using System.Net;
using System.Net.Http.Json;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Metadata;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Artists;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Metadata;

/// <summary>Feature 005 T039: <c>GET /api/tracks/{id}/band-info</c> and <c>POST /api/tracks/{id}/metadata/lookup</c> (FR-008, FR-009).</summary>
public sealed class BandInfoEndpointTests : ApiTestBase
{
    private static readonly Guid RecordingMbid = Guid.Parse("0f4e8a7c-3b2d-4c1e-9f8a-7b6c5d4e3f2a");
    private static readonly Guid ReleaseGroupMbid = Guid.Parse("1a2b3c4d-5e6f-4a7b-8c9d-0e1f2a3b4c5d");

    [Fact]
    public async Task Found_ListsRecordingArtistAndEverySource()
    {
        var track = await SeedFoundAsync(withArtist: true, coverArtArchive: true);

        using var response = await GetBandInfoAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getBandInfo");
        var info = await response.BodyAsync<BandInfoDto>();
        info.Status.ShouldBe(MetadataStatusDto.Found);
        info.Recording.ShouldBe(new RecordingInfoDto("Example Song", "Example Album", 1996));
        var artist = info.Artist!;
        (artist.Mbid, artist.Name, artist.Type, artist.Country, artist.BeginYear).ShouldBe((MetadataSyncTests.ArtistMbid, "The Example Band", ArtistTypeDto.Group, "US", 1981));
        artist.Members.ShouldBe([new ArtistMemberDto("First Member", 1981), new ArtistMemberDto("Second Member", 1983, 2001)]);
        (artist.Summary!.Licence, artist.Summary.HistoryUrl).ShouldBe((WikipediaSummaryDto.CcBySa40, "https://en.wikipedia.org/w/index.php?title=The_Example_Band&action=history"));
        info.Sources.ShouldBe(
        [
            new DataSourceDto(DataSourceDto.MusicBrainz, $"https://musicbrainz.org/recording/{RecordingMbid}", "CC0"),
            new DataSourceDto(DataSourceDto.Wikidata, "https://www.wikidata.org/wiki/Q15920", "CC0"),
            new DataSourceDto(DataSourceDto.Wikipedia, "https://en.wikipedia.org/wiki/The_Example_Band", WikipediaSummaryDto.CcBySa40),
            new DataSourceDto(DataSourceDto.CoverArtArchive, $"https://coverartarchive.org/release-group/{ReleaseGroupMbid}", null),
        ]);
    }

    [Fact]
    public async Task FoundWithoutArtist_ListsOnlyTheRecordingSource()
    {
        var track = await SeedFoundAsync(withArtist: false, coverArtArchive: false);

        using var response = await GetBandInfoAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getBandInfo");
        var info = await response.BodyAsync<BandInfoDto>();
        info.Artist.ShouldBeNull();
        info.Sources.Select(source => source.Name).ShouldBe([DataSourceDto.MusicBrainz]);
    }

    [Fact]
    public async Task SearchingAndFailed_CarryNoFacts()
    {
        var searching = SyncTestData.CompletedTrack("aaaaaaaaaaa");
        searching.ScheduleOwnLookup(SyncTestData.T0);
        searching.StartLookup(MetadataSyncTests.MetadataJobId, SyncTestData.T0);
        var failed = SyncTestData.CompletedTrack("bbbbbbbbbbb");
        failed.ScheduleOwnLookup(SyncTestData.T0);
        failed.FailLookup("Band information could not be retrieved; try again later.", SyncTestData.T0);
        await Factory.SeedAsync(searching);
        await Factory.SeedAsync(failed);

        using var searchingResponse = await GetBandInfoAsync(searching.Id);
        using var failedResponse = await GetBandInfoAsync(failed.Id);

        await ContractAssert.MatchesAsync(searchingResponse, "getBandInfo");
        await ContractAssert.MatchesAsync(failedResponse, "getBandInfo");
        var info = await failedResponse.BodyAsync<BandInfoDto>();
        (info.Status, info.Reason, info.Recording, info.Artist, info.Sources.Count).ShouldBe((MetadataStatusDto.Failed, "Band information could not be retrieved; try again later.", null, null, 0));
        (await searchingResponse.BodyAsync<BandInfoDto>()).Status.ShouldBe(MetadataStatusDto.Searching);
    }

    [Fact]
    public async Task AnOldTrack_IsScheduledWhenItsBandInfoIsOpened()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var info = await (await GetBandInfoAsync(track.Id)).BodyAsync<BandInfoDto>();

        info.Status.ShouldBe(MetadataStatusDto.Pending);
    }

    [Fact]
    public async Task UnknownTrack_Is404()
    {
        using var bandInfo = await GetBandInfoAsync(Guid.NewGuid());
        using var lookup = await PostLookupAsync(Guid.NewGuid(), new MetadataLookupRequest());

        bandInfo.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(bandInfo, "getBandInfo");
        await ContractAssert.MatchesAsync(lookup, "lookupTrackMetadata");
    }

    [Fact]
    public async Task Lookup_WithCorrectedNamesStoresThemAndIsPending()
    {
        var track = await SeedFoundAsync(withArtist: true, coverArtArchive: false);

        using var response = await PostLookupAsync(track.Id, new MetadataLookupRequest(" Hero of the Day ", "Metallica"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Track(track.Id));
        await ContractAssert.MatchesAsync(response, "lookupTrackMetadata");
        var dto = await response.BodyAsync<TrackDto>();
        (dto.Metadata.Status, dto.Metadata.CorrectedByUser).ShouldBe((MetadataStatusDto.Pending, true));
        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.LookupTitle, metadata.LookupArtist).ShouldBe(("Hero of the Day", "Metallica"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("{\"title\":null,\"artist\":null}")]
    public async Task Lookup_WithoutNamesRetries(string? body)
    {
        var track = await SeedFailedAsync();
        using var content = body is null ? null : new StringContent(body, Encoding.UTF8, "application/json");

        using var response = await Client.PostAsync(new Uri(ApiRoutes.MetadataLookup(track.Id), UriKind.Relative), content, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var metadata = (await Factory.FindTrackAsync(track.Id))!.Metadata;
        (metadata.Status, metadata.CorrectedByUser).ShouldBe((MetadataStatus.Pending, false));
    }

    [Fact]
    public async Task Lookup_WhileSearchingIs409()
    {
        var track = SyncTestData.CompletedTrack();
        track.ScheduleOwnLookup(SyncTestData.T0);
        track.StartLookup(MetadataSyncTests.MetadataJobId, SyncTestData.T0);
        await Factory.SeedAsync(track);

        using var response = await PostLookupAsync(track.Id, new MetadataLookupRequest());

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "lookupTrackMetadata");
        (await response.JsonAsync()).GetProperty("title").GetString().ShouldBe(MetadataService.InProgressTitle);
    }

    [Theory]
    [InlineData("", null, "title")]
    [InlineData("  ", null, "title")]
    [InlineData(null, "", "artist")]
    [InlineData("x", "long", "artist")]
    public async Task Lookup_NamesOutsideTheLimitsAre422(string? title, string? artist, string field)
    {
        var track = await SeedFailedAsync();
        var request = new MetadataLookupRequest(title, artist == "long" ? new string('a', 501) : artist);

        using var response = await PostLookupAsync(track.Id, request);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "lookupTrackMetadata");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        (await Factory.FindTrackAsync(track.Id))!.Metadata.Status.ShouldBe(MetadataStatus.Failed);
    }

    private async Task<Track> SeedFailedAsync()
    {
        var track = SyncTestData.CompletedTrack();
        track.ScheduleOwnLookup(SyncTestData.T0);
        track.FailLookup("down", SyncTestData.T0);
        return await Factory.SeedAsync(track);
    }

    private async Task<Track> SeedFoundAsync(bool withArtist, bool coverArtArchive)
    {
        if (withArtist)
        {
            await Factory.WithScopeAsync(async sp =>
            {
                var artists = sp.GetRequiredService<IArtistRepository>();
                artists.Add(Artist.Create(MetadataMapper.ToFacts(MetadataSyncTests.FoundResult().Artist!)));
                await artists.SaveAsync(CancellationToken.None);
                return true;
            });
        }

        var track = SyncTestData.CompletedTrack();
        track.ScheduleOwnLookup(SyncTestData.T0);
        track.StartLookup(MetadataSyncTests.MetadataJobId, SyncTestData.T0);
        var result = MetadataMapper.ToResult(MetadataSyncTests.FoundResult());
        track.CompleteLookup(withArtist ? result : result with { ArtistMbid = null }, SyncTestData.T0);
        if (coverArtArchive)
        {
            track.OfferArtwork(new ArtworkCandidate(ArtworkSource.CoverArtArchive, new string('c', 64)), SyncTestData.T0);
        }

        return await Factory.SeedAsync(track);
    }

    private Task<HttpResponseMessage> GetBandInfoAsync(Guid trackId) => Client.GetAsync(new Uri(ApiRoutes.BandInfo(trackId), UriKind.Relative), Ct);

    private Task<HttpResponseMessage> PostLookupAsync(Guid trackId, MetadataLookupRequest request) =>
        Client.PostAsJsonAsync(new Uri(ApiRoutes.MetadataLookup(trackId), UriKind.Relative), request, Ct);
}
