using System.Net;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;
using StemStudio.Web.Api;
using StemStudio.Web.Tests.Fakes;

namespace StemStudio.Web.Tests.Api;

/// <summary>Lyrics sheets and songs of the typed client (backend contract 1.9.0, feature 010).</summary>
public sealed partial class StemStudioApiClientTests
{
    [Theory]
    [InlineData(null, "/api/lyrics")]
    [InlineData("paper boats", "/api/lyrics?q=paper%20boats")]
    public async Task ListLyricsSheets_SendsTheSearch(string? query, string expected)
    {
        _handler.Respond(HttpStatusCode.OK, Body(new[] { SongData.Sheet() }));

        var sheets = await _client.ListLyricsSheetsAsync(query, Ct);

        sheets.ShouldHaveSingleItem().Title.ShouldBe("Paper boats");
        _handler.Requests.ShouldHaveSingleItem().PathAndQuery.ShouldBe(expected);
    }

    [Fact]
    public async Task LyricsSheetActions_UseTheirRoutes()
    {
        var json = Body(SongData.Sheet());
        _handler.Respond(HttpStatusCode.OK, json).Respond(HttpStatusCode.Created, json).Respond(HttpStatusCode.OK, json).Respond(HttpStatusCode.NoContent);
        var input = new LyricsInput { Title = "Paper boats", Language = "en", Text = SongData.Lyrics };

        await _client.GetLyricsSheetAsync(SongData.SheetId, Ct);
        await _client.CreateLyricsSheetAsync(input, Ct);
        await _client.UpdateLyricsSheetAsync(SongData.SheetId, input, Ct);
        await _client.DeleteLyricsSheetAsync(SongData.SheetId, Ct);

        _handler.Requests.Select(r => (r.Method, r.PathAndQuery)).ShouldBe(
        [
            (HttpMethod.Get, $"/api/lyrics/{SongData.SheetId}"),
            (HttpMethod.Post, "/api/lyrics"),
            (HttpMethod.Put, $"/api/lyrics/{SongData.SheetId}"),
            (HttpMethod.Delete, $"/api/lyrics/{SongData.SheetId}"),
        ]);
        _handler.Requests[1].Body!.ShouldContain("\"language\":\"en\"");
    }

    [Fact]
    public async Task UploadLyricsSheet_SendsTheFileTitleAndLanguage()
    {
        _handler.Respond(HttpStatusCode.Created, Body(SongData.Sheet()));
        await using var stream = new MemoryStream("[00:01.00]Paper boats"u8.ToArray());

        var sheet = await _client.UploadLyricsSheetAsync(new UploadFile(stream, "sample.lrc", "text/plain"), "Paper boats", "en", Ct);

        sheet.Id.ShouldBe(SongData.SheetId);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/lyrics/upload"));
        sent.Parts.Select(p => (p.Name, p.FileName)).ShouldBe([("file", "sample.lrc"), ("title", null), ("language", null)]);
    }

    [Fact]
    public async Task UploadLyricsSheet_LeavesOutEmptyFields()
    {
        _handler.Respond(HttpStatusCode.Created, Body(SongData.Sheet()));
        await using var stream = new MemoryStream("Paper boats"u8.ToArray());

        await _client.UploadLyricsSheetAsync(new UploadFile(stream, "sample.txt", "text/plain"), null, null, Ct);

        _handler.Requests.ShouldHaveSingleItem().Parts.Select(p => p.Name).ShouldBe(["file"]);
    }

    [Fact]
    public async Task ProposeSections_PostsTheText()
    {
        _handler.Respond(HttpStatusCode.OK, Body(SongData.Proposal()));

        var proposal = await _client.ProposeSectionsAsync(SongData.Lyrics, Ct);

        proposal.Sections.Count.ShouldBe(2);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/lyrics/sections"));
        sent.Body!.ShouldStartWith("{\"text\":\"Paper boats");
    }

    [Fact]
    public async Task CreateSong_PostsTheRequest()
    {
        _handler.Respond(HttpStatusCode.Accepted, Body(SongData.Song(GenerationStateDto.Queued)));
        var request = new SongRequest { LyricsText = SongData.Lyrics, Language = "nl", LeadVoice = LeadVoiceDto.Female, TimeSignature = "3", DurationSeconds = 30 };

        var song = await _client.CreateSongAsync(request, Ct);

        song.Kind.ShouldBe(GenerationKindDto.Song);
        var sent = _handler.Requests.ShouldHaveSingleItem();
        (sent.Method, sent.PathAndQuery).ShouldBe((HttpMethod.Post, "/api/songs"));
        sent.Body!.ShouldContain("\"leadVoice\":\"female\"");
        sent.Body!.ShouldContain("\"timeSignature\":\"3\"");
    }
}
