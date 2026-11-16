using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Contracts.Generation;
using StemStudio.Contracts.Lyrics;

namespace StemStudio.Api.Tests.Songs;

/// <summary>010 T034: own lyrics CRUD, upload and search (FR-003).</summary>
public sealed class LyricsSheetEndpointTests : ApiTestBase
{
    private const string LrcFixture = "[00:01.00]Paper boats along the gutter\n[00:04.50]racing past the corner shop\n[00:08.00]every puddle is an ocean\n[00:11.50]and the rain will never stop\n[00:15.00]\n[00:16.00]Sing it loud and sing it slow\n[00:19.50]let the little rivers go\n";
    private const string LrcExpected = "Paper boats along the gutter\nracing past the corner shop\nevery puddle is an ocean\nand the rain will never stop\n\nSing it loud and sing it slow\nlet the little rivers go";

    [Fact]
    public async Task Create_NormalisesTheContractExample()
    {
        using var response = await PostAsync(Input());

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sheet = await response.BodyAsync<LyricsSheetDto>();
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.LyricsSheet(sheet.Id));
        sheet.Text.ShouldBe("[verse]\nSing it loud, sing it free\nEvery note is part of me\n\n\n[chorus]\nThis little song belongs to me");
        await ContractAssert.MatchesAsync(response, "createLyricsSheet");
    }

    [Fact]
    public async Task Create_InvalidFieldsAre400()
    {
        using var response = await PostAsync(Input() with { Title = "", Language = "xx", Text = "[00:01.00]" });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.FieldErrorsAsync("title")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("language")).ShouldNotBeEmpty();
        (await response.FieldErrorsAsync("text")).ShouldNotBeEmpty();
        await ContractAssert.MatchesAsync(response, "createLyricsSheet");
    }

    [Fact]
    public async Task ListGetUpdateDelete()
    {
        var first = await (await PostAsync(Input() with { Title = "Rain song", Text = "paper boats" })).BodyAsync<LyricsSheetDto>();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        var second = await (await PostAsync(Input() with { Title = "Zon", Language = "nl", Text = "mijn lied" })).BodyAsync<LyricsSheetDto>();

        using var all = await GetAsync(ApiRoutes.LyricsSheets);
        using var search = await GetAsync($"{ApiRoutes.LyricsSheets}?q=BOATS");
        using var tooLong = await GetAsync($"{ApiRoutes.LyricsSheets}?q={new string('q', 101)}");
        using var one = await GetAsync(ApiRoutes.LyricsSheet(first.Id));
        using var missing = await GetAsync(ApiRoutes.LyricsSheet(Guid.NewGuid()));
        using var updated = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(first.Id), UriKind.Relative), Input() with { Title = "Renamed" }, Ct);
        using var updateMissing = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(Guid.NewGuid()), UriKind.Relative), Input(), Ct);
        using var updateInvalid = await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(first.Id), UriKind.Relative), Input() with { Title = "" }, Ct);
        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(second.Id), UriKind.Relative), Ct);
        using var deleteMissing = await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(second.Id), UriKind.Relative), Ct);

        (await all.BodyAsync<List<LyricsSheetDto>>()).Select(s => s.Id).ShouldBe([second.Id, first.Id]);
        (await search.BodyAsync<List<LyricsSheetDto>>()).Single().Id.ShouldBe(first.Id);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await updated.BodyAsync<LyricsSheetDto>()).Title.ShouldBe("Renamed");
        (missing.StatusCode, updateMissing.StatusCode, updateInvalid.StatusCode, deleted.StatusCode, deleteMissing.StatusCode)
            .ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound, HttpStatusCode.BadRequest, HttpStatusCode.NoContent, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(all, "listLyricsSheets");
        await ContractAssert.MatchesAsync(tooLong, "listLyricsSheets");
        await ContractAssert.MatchesAsync(one, "getLyricsSheet");
        await ContractAssert.MatchesAsync(missing, "getLyricsSheet");
        await ContractAssert.MatchesAsync(updated, "updateLyricsSheet");
        await ContractAssert.MatchesAsync(updateInvalid, "updateLyricsSheet");
        await ContractAssert.MatchesAsync(deleted, "deleteLyricsSheet");
        await ContractAssert.MatchesAsync(deleteMissing, "deleteLyricsSheet");
    }

    [Theory]
    [InlineData("song.lrc")]
    [InlineData("song.txt")]
    [InlineData("song.md")]
    public async Task Upload_NormalisesTheFile(string fileName)
    {
        using var response = await UploadAsync(Encoding.UTF8.GetBytes(LrcFixture), fileName, title: null, language: "en");

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var sheet = await response.BodyAsync<LyricsSheetDto>();
        (sheet.Title, sheet.Language, sheet.Text).ShouldBe(("song", "en", LrcExpected));
        await ContractAssert.MatchesAsync(response, "uploadLyricsSheet");
    }

    [Fact]
    public async Task Upload_TitleAndLanguageDefaultsAndBom()
    {
        using var response = await UploadAsync([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("la")], "mine.txt", title: "Given", language: null);

        var sheet = await response.BodyAsync<LyricsSheetDto>();
        (sheet.Title, sheet.Language, sheet.Text).ShouldBe(("Given", "unknown", "la"));
    }

    [Fact]
    public async Task Upload_RefusesOtherTypesInvalidUtf8AndLargeFiles()
    {
        using var pdf = await UploadAsync(Encoding.UTF8.GetBytes("%PDF"), "song.pdf", null, null);
        using var binary = await UploadAsync([0xFF, 0xFE, 0x00, 0xC3], "song.txt", null, null);
        using var large = await UploadAsync(new byte[65_537], "song.txt", null, null);
        using var noFile = await Client.PostAsync(new Uri(ApiRoutes.LyricsSheetUpload, UriKind.Relative), new MultipartFormDataContent { { new StringContent("x"), "title" } }, Ct);

        pdf.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
        binary.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await binary.FieldErrorsAsync("file")).ShouldNotBeEmpty();
        large.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        noFile.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        foreach (var response in new[] { pdf, binary, large, noFile })
        {
            await ContractAssert.MatchesAsync(response, "uploadLyricsSheet");
        }
    }

    [Fact]
    public async Task Songs_KeepTheirSnapshotAfterTheSheetChanges()
    {
        var sheet = await (await PostAsync(Input())).BodyAsync<LyricsSheetDto>();
        var song = await (await Client.PostAsJsonAsync(new Uri(ApiRoutes.Songs, UriKind.Relative), new SongRequest { LyricsId = sheet.Id, Language = "en", LeadVoice = LeadVoiceDto.Any }, Ct)).BodyAsync<GenerationDto>();

        (await Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Input() with { Text = "changed" }, Ct)).Dispose();
        (await Client.DeleteAsync(new Uri(ApiRoutes.LyricsSheet(sheet.Id), UriKind.Relative), Ct)).Dispose();

        var stored = await (await GetAsync(ApiRoutes.Generation(song.Id))).BodyAsync<GenerationDto>();
        stored.Song!.Lyrics.Text.ShouldBe(sheet.Text);
    }

    private static LyricsInput Input() => new()
    {
        Title = "This little song",
        Language = "en",
        Text = "[verse]\r\nSing it loud, sing it free   \r\nEvery note is part of me\r\n\r\n\r\n\r\n[chorus]\r\nThis little song belongs to me",
    };

    private Task<HttpResponseMessage> PostAsync(LyricsInput input) => Client.PostAsJsonAsync(new Uri(ApiRoutes.LyricsSheets, UriKind.Relative), input, Ct);

    private Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private Task<HttpResponseMessage> UploadAsync(byte[] bytes, string fileName, string? title, string? language)
    {
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        var content = new MultipartFormDataContent { { file, LyricsUploadForm.File, fileName } };
        if (title is not null)
        {
            content.Add(new StringContent(title), LyricsUploadForm.Title);
        }

        if (language is not null)
        {
            content.Add(new StringContent(language), LyricsUploadForm.Language);
        }

        return Client.PostAsync(new Uri(ApiRoutes.LyricsSheetUpload, UriKind.Relative), content, Ct);
    }
}
