using System.Net;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Lyrics operations of the typed client (backend contract 1.6.0, feature 006).</summary>
public sealed partial class StemStudioApiClientTests
{
    private static string LyricsPath => $"/api/tracks/{TestData.TrackId}/lyrics";

    [Fact]
    public async Task GetLyrics_GetsTheLyricsResource()
    {
        _handler.Respond(HttpStatusCode.OK, Json(LyricsData.Found()));

        var lyrics = await _client.GetLyricsAsync(TestData.TrackId, Ct);

        lyrics.Status.ShouldBe(LyricsStatusDto.Found);
        lyrics.Lines[0].Words[0].StartMs.ShouldBe(10950);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Get, LyricsPath));
    }

    [Fact]
    public async Task SetLyricsOffset_PutsTheOffset()
    {
        _handler.Respond(HttpStatusCode.OK, Json(LyricsData.Found(offsetMs: 250)));

        var lyrics = await _client.SetLyricsOffsetAsync(TestData.TrackId, 250, Ct);

        lyrics.OffsetMs.ShouldBe(250);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery, sent.Body).ShouldBe((HttpMethod.Put, $"{LyricsPath}/offset", "{\"offsetMs\":250}"));
    }

    [Fact]
    public async Task SetLyricsOffset_WithoutLyrics_ThrowsTheConflict()
    {
        _handler.RespondProblem(HttpStatusCode.Conflict, """{"type":"lyrics-none","title":"The track has no lyrics.","status":409}""");

        var error = await Should.ThrowAsync<ApiException>(() => _client.SetLyricsOffsetAsync(TestData.TrackId, 10, Ct));

        error.Error.IsConflict.ShouldBeTrue();
        error.Error.UserMessage.ShouldBe("The track has no lyrics.");
    }
}
