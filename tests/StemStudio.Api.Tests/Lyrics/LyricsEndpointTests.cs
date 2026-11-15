using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;
using static StemStudio.Api.Tests.TestSupport.LyricsTestData;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>Feature 006 T033: <c>GET /api/tracks/{id}/lyrics</c>, the offset, and lyrics never leaving their resource (FR-007, FR-010, SC-006).</summary>
public sealed class LyricsEndpointTests : ApiTestBase
{
    [Fact]
    public async Task Get_FoundLyricsMatchTheContractExampleWithNoStore()
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await GetAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "getLyrics");
        var example = OpenApiContract.Backend.Root["components"]!["examples"]!["FoundLyrics"]!["value"]!;
        JsonNode.DeepEquals(JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct)), example).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_PlainLyricsShowTheTextWithTheAlignment()
    {
        var track = await Factory.SeedAsync(AlignPending());

        using var response = await GetAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getLyrics");
        var dto = await response.BodyAsync<LyricsDto>();
        (dto.Status, dto.SyncLevel, dto.Origin, dto.Credit).ShouldBe((LyricsStatusDto.AlignPending, SyncLevelDto.Plain, LyricsOriginDto.None, "Lyrics: LRCLIB"));
        dto.Lines.Select(line => line.Text).ShouldBe(FixtureWords);
        dto.Lines[0].Words.ShouldAllBe(word => word.StartMs == null && word.Origin == null);
        dto.Alignment.ShouldBe(new LyricsAlignmentDto(null, null, null));
    }

    [Theory]
    [InlineData("lyrics-result-line", "Lyrics: LRCLIB; word timing estimated")]
    [InlineData("lyrics-result-instrumental", "Lyrics: LRCLIB")]
    public async Task Get_OtherDocumentsCarryTheirCredit(string example, string credit)
    {
        var track = await Factory.SeedAsync(WithResult(example));

        using var response = await GetAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getLyrics");
        (await response.BodyAsync<LyricsDto>()).Credit.ShouldBe(credit);
    }

    [Fact]
    public async Task Get_NotFoundLyricsMatchTheContractExample()
    {
        var track = await Factory.SeedAsync(WithResult("lyrics-result-not-found"));

        using var response = await GetAsync(track.Id);

        var example = OpenApiContract.Backend.Root["components"]!["examples"]!["NotFoundLyrics"]!["value"]!;
        JsonNode.DeepEquals(JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct)), example).ShouldBeTrue();
    }

    [Fact]
    public async Task Get_ATrackStillProcessingHasNoLyricsAndStartsNothing()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        using var response = await GetAsync(track.Id);

        await ContractAssert.MatchesAsync(response, "getLyrics");
        (await response.BodyAsync<LyricsDto>()).ShouldBe(new LyricsDto { Status = LyricsStatusDto.None }, new LyricsDtoComparer());
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.None);
    }

    [Fact]
    public async Task Get_AnOldCompletedTrackStartsTheSearch()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await GetAsync(track.Id);

        (await response.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Searching);
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.Searching);
    }

    [Fact]
    public async Task Get_UnknownTrackIs404()
    {
        using var response = await GetAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "getLyrics");
    }

    [Theory]
    [InlineData(-120)]
    [InlineData(5000)]
    [InlineData(-5000)]
    public async Task SetOffset_StoresTheOffset(int offset)
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await PutOffsetAsync(track.Id, offset);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "setLyricsOffset");
        (await response.BodyAsync<LyricsDto>()).OffsetMs.ShouldBe(offset);
        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.OffsetMs.ShouldBe(offset);
    }

    [Theory]
    [InlineData(5010)]
    [InlineData(-5010)]
    [InlineData(125)]
    public async Task SetOffset_OutsideTheLimitsIs422(int offset)
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await PutOffsetAsync(track.Id, offset);

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "setLyricsOffset");
        (await response.FieldErrorsAsync("offsetMs")).ShouldNotBeEmpty();
    }

    [Fact]
    public async Task SetOffset_WithoutLyricsIs409()
    {
        var notFound = await Factory.SeedAsync(WithResult("lyrics-result-not-found"));
        var none = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));

        using var response = await PutOffsetAsync(notFound.Id, 100);
        using var noRow = await PutOffsetAsync(none.Id, 100);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "setLyricsOffset");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("lyrics-none");
        noRow.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await Factory.FindTrackAsync(none.Id))!.LyricsStatus.ShouldBe(LyricsStatus.None);
    }

    [Fact]
    public async Task SetOffset_UnknownTrackIs404()
    {
        using var response = await PutOffsetAsync(Guid.NewGuid(), 100);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "setLyricsOffset");
    }

    [Fact]
    public async Task TrackResources_CarryTheStatusButNeverTheText()
    {
        var track = await Factory.SeedAsync(Found());

        var list = await Client.GetStringAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);
        var single = await Client.GetStringAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        foreach (var body in new[] { list, single })
        {
            body.ShouldContain("\"lyricsStatus\":\"found\"");
            FixtureWords.ShouldAllBe(text => !body.Contains(text, StringComparison.Ordinal));
            body.ShouldNotContain("\"Fake\"");
        }
    }

    [Fact]
    public async Task AFullFakeRun_NeverLogsTheLyrics()
    {
        using var factory = new ApiFactory { ConfigureServices = services => services.Configure<LoggerFilterOptions>(options => options.AddFilter<ListLoggerProvider>(null, LogLevel.Trace)) };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(SearchingWithJob());
        factory.Worker.OnGetLyricsJob = id => Task.FromResult<WorkerLyricsJob?>(FakeWorkerClient.LyricsJob(id, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Completed, 1));
        factory.Worker.OnGetLyricsResult = _ => Task.FromResult(Result("lyrics-result-word"));

        await factory.SyncLyricsAsync();
        (await client.GetStringAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct)).ShouldContain(FixtureWords[0]);
        (await client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsOffset(track.Id), UriKind.Relative), new LyricsOffsetRequest(-120), Ct)).Dispose();
        (await client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsOffset(track.Id), UriKind.Relative), new LyricsOffsetRequest(7), Ct)).Dispose();

        var messages = factory.Logs.Entries.Select(entry => entry.Message + entry.Exception).ToList();
        factory.Logs.Entries.ShouldContain(entry => entry.Level <= LogLevel.Debug);
        messages.ShouldAllBe(message => !FixtureWords.Any(words => message.Contains(words, StringComparison.Ordinal)));
        messages.ShouldContain(message => message.Contains("Searching -> Found", StringComparison.Ordinal));
    }

    [Fact]
    public void DocumentsAndRequests_DoNotPrintTheirTextWhenLogged()
    {
        var document = Document("lyrics-result-word");
        var printed = string.Join(" | ", document, document.Lines[0], document.Lines[0].Words[0], new WorkerLyricsAlignRequest { StandardJobId = StandardJobId, Text = "Fake line one", Refine = document }, Result("lyrics-result-word"));

        printed.ShouldNotContain("Fake");
        printed.ShouldContain("SyncLevel = Word");
        printed.ShouldContain("Lines = 2");
    }

    private Task<HttpResponseMessage> GetAsync(Guid trackId) => Client.GetAsync(new Uri(ApiRoutes.Lyrics(trackId), UriKind.Relative), Ct);

    private Task<HttpResponseMessage> PutOffsetAsync(Guid trackId, int offset) =>
        Client.PutAsJsonAsync(new Uri(ApiRoutes.LyricsOffset(trackId), UriKind.Relative), new LyricsOffsetRequest(offset), Ct);

    /// <summary>Compares two DTOs by their JSON form (the lines list has no value equality).</summary>
    private sealed class LyricsDtoComparer : IEqualityComparer<LyricsDto>
    {
        public bool Equals(LyricsDto? x, LyricsDto? y) => System.Text.Json.JsonSerializer.Serialize(x) == System.Text.Json.JsonSerializer.Serialize(y);

        public int GetHashCode(LyricsDto obj) => System.Text.Json.JsonSerializer.Serialize(obj).GetHashCode(StringComparison.Ordinal);
    }
}
