using System.Net;
using StemStudio.Contracts.Artists;
using StemStudio.Contracts.Tracks;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Band info and lookup operations of the typed client (backend contract 1.4.0).</summary>
public sealed partial class StemStudioApiClientTests
{
    private const string FoundBandInfoJson = """
        {
          "status": "found", "reason": null,
          "recording": { "title": "Hero of the Day", "album": "Load", "year": 1996 },
          "artist": {
            "mbid": "65f4f0c5-ef9e-490c-aee3-909e7ae6b2ab", "name": "Metallica", "type": "group", "country": "US",
            "beginYear": 1981, "endYear": null, "genres": ["heavy metal"], "members": [ { "name": "James Hetfield", "fromYear": 1981, "toYear": null } ],
            "website": "https://www.metallica.com/",
            "summary": { "language": "en", "title": "Metallica", "extract": "Placeholder extract.", "pageUrl": "https://en.wikipedia.org/wiki/Metallica",
                         "historyUrl": "https://en.wikipedia.org/w/index.php?title=Metallica&action=history", "licence": "CC BY-SA 4.0",
                         "licenceUrl": "https://creativecommons.org/licenses/by-sa/4.0/" }
          },
          "sources": [ { "name": "MusicBrainz", "url": "https://musicbrainz.org/recording/x", "licence": "CC0" } ]
        }
        """;

    [Fact]
    public async Task GetBandInfo_GetsTheBandInfoRoute()
    {
        _handler.Respond(HttpStatusCode.OK, FoundBandInfoJson);

        var info = await _client.GetBandInfoAsync(TestData.TrackId, Ct);

        info.Status.ShouldBe(MetadataStatusDto.Found);
        info.Artist!.Type.ShouldBe(ArtistTypeDto.Group);
        info.Artist.Summary!.Title.ShouldBe("Metallica");
        info.Recording!.Year.ShouldBe(1996);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Get);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/band-info");
    }

    [Fact]
    public async Task LookupMetadata_WithNames_PostsThemAndReturnsTheTrack()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);

        var track = await _client.LookupMetadataAsync(TestData.TrackId, new MetadataLookupRequest("Hero of the Day", "Metallica"), Ct);

        track.Id.ShouldBe(TestData.TrackId);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/metadata/lookup");
        request.Body.ShouldBe("""{"title":"Hero of the Day","artist":"Metallica"}""");
    }

    [Fact]
    public async Task LookupMetadata_Retry_PostsAnEmptyObject()
    {
        _handler.Respond(HttpStatusCode.Accepted, TestData.CompletedTrackJson);

        await _client.LookupMetadataAsync(TestData.TrackId, null, Ct);

        _handler.Requests.ShouldHaveSingleItem().Body.ShouldBe("{}");
    }

    [Fact]
    public async Task LookupMetadata_InProgress_ThrowsTheConflict()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"title":"Lookup in progress","status":409,"detail":"A lookup is already running."}""");

        var error = (await Should.ThrowAsync<ApiException>(() => _client.LookupMetadataAsync(TestData.TrackId, null, Ct))).Error;

        error.IsConflict.ShouldBeTrue();
        error.UserMessage.ShouldBe("A lookup is already running.");
    }

    [Fact]
    public async Task SetArtwork_PutsTheFileAsMultipart_AndReturnsTheTrack()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.CompletedTrackJson);
        await using var stream = new MemoryStream(new byte[4096]);

        var track = await _client.SetArtworkAsync(TestData.TrackId, new UploadFile(stream, "own.png", "image/png"), Ct);

        track.Id.ShouldBe(TestData.TrackId);
        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Put);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/artwork");
        var part = request.Parts.ShouldHaveSingleItem();
        (part.Name, part.FileName, part.Length).ShouldBe(("file", "own.png", 4096L));
    }

    [Fact]
    public async Task ResetArtwork_DeletesTheOwnPicture()
    {
        _handler.Respond(HttpStatusCode.OK, TestData.CompletedTrackJson);

        await _client.ResetArtworkAsync(TestData.TrackId, Ct);

        var request = _handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Delete);
        request.PathAndQuery.ShouldBe($"/api/tracks/{TestData.TrackId}/artwork");
    }
}
