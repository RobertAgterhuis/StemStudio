using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Tracks;

public sealed class TrackEndpointsTests : IDisposable
{
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public TrackEndpointsTests() => _client = _factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task ListTracks_EmptyReturnsEmptyArray()
    {
        using var response = await _client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<List<TrackDto>>()).ShouldBeEmpty();
    }

    [Fact]
    public async Task ListTracks_OrderedByUpdatedAtDescending()
    {
        var older = await _factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa", SyncTestData.T0.AddMinutes(1)));
        var newest = await _factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb", SyncTestData.T0.AddMinutes(9)));
        var middle = await _factory.SeedAsync(SyncTestData.CompletedTrack("ccccccccccc", SyncTestData.T0.AddMinutes(5)));

        using var response = await _client.GetAsync(new Uri(ApiRoutes.Tracks, UriKind.Relative), Ct);

        (await response.BodyAsync<List<TrackDto>>()).Select(t => t.Id).ShouldBe([newest.Id, middle.Id, older.Id]);
    }

    [Fact]
    public async Task GetTrack_CompletedTrackHasRelativeStemUrlsAndBeatGrid()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await _client.GetAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await response.BodyAsync<TrackDto>();
        dto.Status.ShouldBe(TrackStatusDto.Completed);
        dto.Progress.ShouldBe(100);
        dto.Model.ShouldBe("htdemucs_6s");
        dto.DurationSeconds.ShouldBe(213.04);
        dto.Stems.Select(s => s.Name).ShouldBe(StemNames.SixStem);
        dto.Stems[0].Url.ShouldBe($"/api/tracks/{track.Id}/stems/vocals");
        dto.BeatGrid!.Bpm.ShouldBe(113.5);
        dto.BeatGrid.Reliable.ShouldBeTrue();
        dto.CreatedAt.Offset.ShouldBe(TimeSpan.Zero);
        dto.UpdatedAt.ShouldBe(ApiFactory.Start);
    }

    [Fact]
    public async Task GetTrack_UnknownReturns404Problem()
    {
        var id = Guid.NewGuid();

        using var response = await _client.GetAsync(new Uri(ApiRoutes.Track(id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe($"Track {id} does not exist.");
    }

    [Fact]
    public async Task GetTrack_NonGuidReturns404()
    {
        using var response = await _client.GetAsync(new Uri("/api/tracks/not-a-guid", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_FailedUrlTrack_ResendsCanonicalUrlAndQueuesWithNewJob()
    {
        var track = await _factory.SeedAsync(SyncTestData.FailedTrack());

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.ToString().ShouldBe(ApiRoutes.Track(track.Id));
        var dto = await response.BodyAsync<TrackDto>();
        dto.Status.ShouldBe(TrackStatusDto.Queued);
        dto.Error.ShouldBeNull();
        dto.Progress.ShouldBe(0);
        _factory.Worker.UrlRequests.Single().ShouldBe(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"));
        var stored = (await _factory.FindTrackAsync(track.Id))!;
        stored.WorkerJobId.ShouldNotBeNull();
        stored.WorkerJobId.ShouldNotBe(track.WorkerJobId);
    }

    [Fact]
    public async Task Retry_NotFailed_Returns409()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await response.JsonAsync();
        problem.GetProperty("title").GetString().ShouldBe("Track is not failed");
        _factory.Worker.CreateCalls.ShouldBe(0);
    }

    [Fact]
    public async Task Retry_Unknown_Returns404()
    {
        using var response = await PostRetryAsync(Guid.NewGuid());

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Retry_UploadWithFile_StreamsTheKeptFileAgain()
    {
        var uploads = _factory.Services.GetRequiredService<IUploadStore>();
        var stored = await uploads.SaveAsync(new MemoryStream([9, 9, 9]), ".flac", Ct);
        var track = await _factory.SeedAsync(FailedUpload(stored));

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var upload = _factory.Worker.Uploads.Single();
        upload.Upload.FileName.ShouldBe(stored.FileName);
        upload.Bytes.ShouldBe([9, 9, 9]);
    }

    [Fact]
    public async Task Retry_UploadWhoseFileIsGone_Returns409()
    {
        var uploads = _factory.Services.GetRequiredService<IUploadStore>();
        var stored = await uploads.SaveAsync(new MemoryStream([1]), ".mp3", Ct);
        var track = await _factory.SeedAsync(FailedUpload(stored));
        uploads.Delete(stored.FileName);

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString()!.ShouldContain("upload it again");
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Failed);
    }

    [Fact]
    public async Task Retry_WorkerUnavailable_Returns503AndTrackStaysFailed()
    {
        var track = await _factory.SeedAsync(SyncTestData.FailedTrack());
        _factory.Worker.OnCreateUrl = _ => throw new WorkerUnavailableException();

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        var stored = (await _factory.FindTrackAsync(track.Id))!;
        stored.Status.ShouldBe(TrackStatus.Failed);
        stored.Error.ShouldBe("interrupted by restart");
    }

    [Fact]
    public async Task Retry_WorkerRejects_Returns409WithDetail()
    {
        var track = await _factory.SeedAsync(SyncTestData.FailedTrack());
        _factory.Worker.OnCreateUrl = _ => throw new WorkerRequestException("Video unavailable.");

        using var response = await PostRetryAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("Video unavailable.");
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Failed);
    }

    [Theory]
    [InlineData("http://web.test", true)]
    [InlineData("http://evil.test", false)]
    public async Task Cors_AllowsOnlyTheWebOrigin(string origin, bool allowed)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ApiRoutes.Tracks);
        request.Headers.Add("Origin", origin);

        using var response = await _client.SendAsync(request, Ct);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBe(allowed);
    }

    private Task<HttpResponseMessage> PostRetryAsync(Guid id) =>
        _client.PostAsync(new Uri(ApiRoutes.RetryTrack(id), UriKind.Relative), null, Ct);

    private static Track FailedUpload(StoredUpload stored)
    {
        var track = Track.FromUpload(Guid.NewGuid(), SourceKey.ForUpload(stored.Sha256), new UploadedFile("a" + Path.GetExtension(stored.FileName), stored.FileName), SyncTestData.T0);
        track.AssignJob("0123456789abcdef0123456789abcdef", SyncTestData.T0);
        track.Fail("interrupted by restart", SyncTestData.T0);
        return track;
    }
}
