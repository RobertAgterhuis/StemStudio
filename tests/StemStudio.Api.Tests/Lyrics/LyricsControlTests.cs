using System.Net;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Domain.Lyrics;
using static StemStudio.Api.Tests.TestSupport.LyricsTestData;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>Feature 006 T049: removing the lyrics and searching again (FR-009; data-model 3.2).</summary>
public sealed class LyricsControlTests : ApiTestBase
{
    [Fact]
    public async Task Remove_ClearsTheLyricsAndMatchesTheContractExample()
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await Client.DeleteAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "removeLyrics");
        var example = OpenApiContract.Backend.Root["components"]!["examples"]!["RemovedLyrics"]!["value"]!;
        System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(await response.Content.ReadAsStringAsync(Ct)), example).ShouldBeTrue();
        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.Document.ShouldBeNull();
    }

    [Fact]
    public async Task Remove_StopsARunningJobAndNoAutomaticLookupFollows()
    {
        var track = await Factory.SeedAsync(SearchingWithJob());

        (await Client.DeleteAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct)).Dispose();
        await Factory.SyncLyricsAsync();
        await Client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);
        using var lyrics = await Client.GetAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct);

        Worker.LyricsDeletes.ShouldBe([LookupJobId]);
        Worker.LyricsLookups.ShouldBeEmpty();
        (await lyrics.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Removed);
    }

    [Fact]
    public async Task Remove_OfATrackWithoutLyricsAndOfAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());
        Worker.OnDeleteLyrics = _ => throw new WorkerUnavailableException();

        using var removed = await Client.DeleteAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct);
        using var unknown = await Client.DeleteAsync(new Uri(ApiRoutes.Lyrics(Guid.NewGuid()), UriKind.Relative), Ct);

        (await removed.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Removed);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "removeLyrics");
        Worker.OnGetJob = id => Task.FromResult<WorkerJob?>(FakeWorkerClient.Job(id, WorkerJobState.Separating, 40) with { Metadata = new WorkerSourceMetadata("Song", null, null, DurationSeconds: 200) });
        await Factory.SyncTracksAsync();
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.Removed);
    }

    [Theory]
    [InlineData("removed")]
    [InlineData("lyrics-result-not-found")]
    [InlineData("lyrics-result-disabled")]
    [InlineData("failed")]
    [InlineData("lyrics-result-word")]
    public async Task Search_StartsANewLookup(string from)
    {
        var track = await Factory.SeedAsync(TrackIn(from));

        using var response = await Client.PostAsync(new Uri(ApiRoutes.LyricsSearch(track.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "searchLyrics");
        (await response.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Searching);
        await Factory.SyncLyricsAsync();
        Worker.LyricsLookups.ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData("searching")]
    [InlineData("aligning")]
    public async Task Search_WhileTheWorkerRunsIs409(string from)
    {
        var track = await Factory.SeedAsync(from == "searching" ? SearchingWithJob() : Aligning());

        using var response = await Client.PostAsync(new Uri(ApiRoutes.LyricsSearch(track.Id), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "searchLyrics");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe("lyrics-busy");
    }

    [Fact]
    public async Task Search_UnknownTrackIs404()
    {
        using var response = await Client.PostAsync(new Uri(ApiRoutes.LyricsSearch(Guid.NewGuid()), UriKind.Relative), null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "searchLyrics");
    }

    private static Domain.Tracks.Track TrackIn(string from)
    {
        switch (from)
        {
            case "removed":
                var removed = Found();
                removed.Lyrics!.Remove(SyncTestData.T0);
                return removed;
            case "failed":
                var failed = SearchingWithJob();
                failed.Lyrics!.Fail("Lyrics could not be retrieved; try again later.", SyncTestData.T0);
                return failed;
            default:
                return WithResult(from);
        }
    }
}
