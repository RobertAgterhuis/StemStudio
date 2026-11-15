using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Contract;

/// <summary>T053: <c>Track.detail</c> in every status and <c>getDetailStem</c> against backend-api.openapi.yaml 1.2.0 (FR-007).</summary>
public sealed partial class BackendContractTests
{
    [Fact]
    public async Task GetAndListTracks_WithEveryDetailStatusMatchContract()
    {
        var tracks = await SeedEveryDetailStatusAsync();

        foreach (var track in tracks)
        {
            using var response = await GetAsync(ApiRoutes.Track(track.Id));
            await ContractAssert.MatchesAsync(response, "getTrack");
        }

        await ContractAssert.MatchesAsync(await GetAsync(ApiRoutes.Tracks), "listTracks");
    }

    [Fact]
    public async Task GetTrack_MapsEveryDetailStatus()
    {
        var tracks = await SeedEveryDetailStatusAsync();

        var statuses = new List<string>();
        foreach (var track in tracks)
        {
            var json = await (await GetAsync(ApiRoutes.Track(track.Id))).JsonAsync();
            statuses.Add(json.GetProperty("detail").GetProperty("status").GetString()!);
        }

        statuses.ShouldBe(["none", "queued", "running", "completed", "failed", "cancelled"]);
    }

    [Fact]
    public async Task GetTrack_NoneDetailPhaseHasTheContractDefaults()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        var detail = (await (await GetAsync(ApiRoutes.Track(track.Id))).JsonAsync()).GetProperty("detail");

        detail.GetProperty("progress").GetInt32().ShouldBe(0);
        detail.GetProperty("parents").GetArrayLength().ShouldBe(0);
        detail.GetProperty("updatedAt").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetTrack_RunningDetailPhaseLikeTheContract()
    {
        var track = await _factory.SeedAsync(Running("aaaaaaaaaaa"));

        var detail = (await (await GetAsync(ApiRoutes.Track(track.Id))).JsonAsync()).GetProperty("detail");

        detail.GetProperty("stage").GetString().ShouldBe("Splitting drums (1 of 3)");
        detail.GetProperty("currentParent").GetString().ShouldBe("drums");
        detail.GetProperty("progress").GetInt32().ShouldBe(12);
        detail.GetProperty("updatedAt").GetDateTimeOffset().ShouldBe(ApiFactory.Start.AddMinutes(2));
    }

    [Fact]
    public async Task GetTrack_CompletedDetailPhaseLikeTheDetailCompletedTrackExample()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        var detail = (await (await GetAsync(ApiRoutes.Track(track.Id))).JsonAsync()).GetProperty("detail");

        detail.GetProperty("progress").GetInt32().ShouldBe(100);
        detail.GetProperty("error").ValueKind.ShouldBe(JsonValueKind.Null);
        var parents = detail.GetProperty("parents").EnumerateArray().ToList();
        parents.Select(p => p.GetProperty("outcome").GetString()).ShouldBe(["split", "split", "not-supported", "nothing-further", "skipped-silent", "not-reliable"]);
        var drums = parents[1].GetProperty("stems").EnumerateArray().ToList();
        drums.Select(s => s.GetProperty("name").GetString()).ShouldBe(["kick", "snare", "rest"]);
        drums[0].GetProperty("url").GetString().ShouldBe(ApiRoutes.DetailStem(track.Id, "drums", "kick"));
        parents[2].GetProperty("stems").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task GetTrack_FailedDetailPhaseCarriesTheError()
    {
        var track = SyncTestData.DetailQueued();
        track.FailDetail("interrupted by restart", SyncTestData.T0.AddMinutes(1));
        await _factory.SeedAsync(track);

        var detail = (await (await GetAsync(ApiRoutes.Track(track.Id))).JsonAsync()).GetProperty("detail");

        detail.GetProperty("error").GetString().ShouldBe("interrupted by restart");
    }

    [Fact]
    public async Task GetDetailStem_EveryStatusMatchesContract()
    {
        var track = await SeedDetailWithFileAsync();
        var path = ApiRoutes.DetailStem(track.Id, "drums", "kick");

        using var full = await GetAsync(path);
        using var partial = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(0, 99));
        using var notModified = await SendAsync(path, request => request.Headers.IfNoneMatch.Add(full.Headers.ETag!));
        using var unsatisfiable = await SendAsync(path, request => request.Headers.Range = new RangeHeaderValue(5000, 6000));
        using var notFound = await GetAsync(ApiRoutes.DetailStem(track.Id, "drums", "toms"));

        (full.StatusCode, partial.StatusCode, notModified.StatusCode, unsatisfiable.StatusCode, notFound.StatusCode).ShouldBe(
            (HttpStatusCode.OK, HttpStatusCode.PartialContent, HttpStatusCode.NotModified, HttpStatusCode.RequestedRangeNotSatisfiable, HttpStatusCode.NotFound));
        foreach (var response in new[] { full, partial, notModified, unsatisfiable, notFound })
        {
            await ContractAssert.MatchesAsync(response, "getDetailStem");
        }
    }

    [Fact]
    public async Task GetDetailStem_ServesTheFlacWithStaticFileHeaders()
    {
        var track = await SeedDetailWithFileAsync();

        using var response = await GetAsync(ApiRoutes.DetailStem(track.Id, "drums", "kick"));

        response.Content.Headers.ContentType!.MediaType.ShouldBe("audio/flac");
        response.Content.Headers.ContentLength.ShouldBe(2048);
        response.Headers.AcceptRanges.ShouldContain("bytes");
        response.Content.Headers.LastModified.ShouldNotBeNull();
    }

    [Theory]
    [InlineData("drums", "lead")]
    [InlineData("bass", "rest")]
    [InlineData("drums", "Kick")]
    [InlineData("guitar", "strings")]
    public async Task GetDetailStem_404ForStemsTheTrackDoesNotHave(string parent, string name)
    {
        var track = await SeedDetailWithFileAsync();

        using var response = await GetAsync(ApiRoutes.DetailStem(track.Id, parent, name));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "getDetailStem");
    }

    [Fact]
    public async Task GetDetailStem_404ForAnUnknownTrackOrAPhaseThatIsNotCompleted()
    {
        var queued = await _factory.SeedAsync(SyncTestData.DetailQueued("bbbbbbbbbbb"));

        using var unknown = await GetAsync(ApiRoutes.DetailStem(Guid.NewGuid(), "drums", "kick"));
        using var notCompleted = await GetAsync(ApiRoutes.DetailStem(queued.Id, "drums", "kick"));

        (unknown.StatusCode, notCompleted.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(unknown, "getDetailStem");
    }

    [Fact]
    public async Task GetDetailStem_404WhenTheFileIsMissing()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());

        using var response = await GetAsync(ApiRoutes.DetailStem(track.Id, "vocals", "lead"));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("http://web.test", true)]
    [InlineData("http://evil.test", false)]
    public async Task GetDetailStem_CorsAllowsTheWebOriginAndExposesRangeHeaders(string origin, bool allowed)
    {
        var track = await SeedDetailWithFileAsync();

        using var response = await SendAsync(ApiRoutes.DetailStem(track.Id, "drums", "kick"), request =>
        {
            request.Headers.Add("Origin", origin);
            request.Headers.Range = new RangeHeaderValue(0, 1);
        });

        response.Headers.TryGetValues("Access-Control-Allow-Origin", out var origins).ShouldBe(allowed);
        if (allowed)
        {
            origins!.Single().ShouldBe("http://web.test");
            var exposed = string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers"));
            exposed.ShouldContain("Content-Range");
            exposed.ShouldContain("Accept-Ranges");
            exposed.ShouldContain("ETag");
        }
    }

    private async Task<Track> SeedDetailWithFileAsync()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailCompleted());
        await _factory.Services.GetRequiredService<IDetailStemFileStore>()
            .SaveAsync(new DetailStemKey(track.Id, "drums", "kick"), (s, ct) => s.WriteAsync(new byte[2048], ct).AsTask(), Ct);
        return track;
    }

    private async Task<List<Track>> SeedEveryDetailStatusAsync()
    {
        var failed = SyncTestData.DetailQueued("ddddddddddd");
        failed.FailDetail("Not enough memory for detail separation.", SyncTestData.T0.AddMinutes(1));
        var cancelled = SyncTestData.DetailQueued("eeeeeeeeeee", detailJobId: null);
        cancelled.CancelDetail(SyncTestData.T0.AddMinutes(1));
        var tracks = new List<Track>
        {
            SyncTestData.CompletedTrack("zzzzzzzzzzz"), SyncTestData.DetailQueued("aaaaaaaaaaa", detailJobId: null), Running("bbbbbbbbbbb"),
            SyncTestData.DetailCompleted("ccccccccccc"), failed, cancelled,
        };
        foreach (var track in tracks)
        {
            await _factory.SeedAsync(track);
        }

        return tracks;
    }

    private static Track Running(string videoId)
    {
        var track = SyncTestData.DetailQueued(videoId);
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), SyncTestData.T0.AddMinutes(2));
        return track;
    }
}
