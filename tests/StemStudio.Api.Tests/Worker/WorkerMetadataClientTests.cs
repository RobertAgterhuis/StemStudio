using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 005 T020: artwork, metadata job and normalise calls of the worker client against the worker 1.3.0 examples.</summary>
public sealed class WorkerMetadataClientTests
{
    private const string MetadataJobId = "5d2c7e9f1a3b4c6d8e0f2a4b6c8d0e1f";
    private const string ArtworkId = "8e4f2a6c0b1d4e3f9a7c5b3d1e0f2a4c";
    private const string JobId = WorkerExamples.JobId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetJob_ParsesArtworkAndTheNewSourceMetadataFields()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "completed-job"));

        var job = (await client.GetJobAsync(JobId, Ct))!;

        job.Artwork.ShouldBe(new WorkerArtworkInfo(
            WorkerArtworkSource.Youtube, "9b74c9897bac770ffc029102a200c5de2c2f6f8b6e8e0b4f3b8a5a0f52a3c1d2", 48210, 6120, "https://i.ytimg.com/vi/dQw4w9WgXcQ/maxresdefault.jpg"));
        var metadata = job.Metadata!;
        (metadata.Album, metadata.Isrc, metadata.DurationSeconds).ShouldBe((null, null, 213.04));
        (metadata.LookupTitle, metadata.LookupArtist).ShouldBe(("Never Gonna Give You Up", "Rick Astley"));
    }

    [Fact]
    public async Task GetJob_A120BodyWithoutTheNewFieldsParsesToNull()
    {
        const string Old = """{"jobId":"3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c","sourceKey":"youtube:dQw4w9WgXcQ","status":"separating","progress":10,"metadata":{"title":"T","artist":null,"sourceUrl":null}}""";
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, Old));

        var job = (await client.GetJobAsync(JobId, Ct))!;

        job.Artwork.ShouldBeNull();
        (job.Metadata!.Album, job.Metadata.Isrc, job.Metadata.DurationSeconds, job.Metadata.LookupTitle, job.Metadata.LookupArtist).ShouldBe((null, null, null, null, null));
    }

    [Theory]
    [InlineData("""{"source":"spotify","sha256":"x","coverBytes":1,"thumbBytes":1}""", WorkerArtworkSource.Spotify)]
    [InlineData("""{"source":"embedded","sha256":"x","coverBytes":1,"thumbBytes":1}""", WorkerArtworkSource.Embedded)]
    [InlineData("""{"source":"coverartarchive","sha256":"x","coverBytes":1,"thumbBytes":1}""", WorkerArtworkSource.CoverArtArchive)]
    [InlineData("""{"source":"user","sha256":"x","coverBytes":1,"thumbBytes":1}""", WorkerArtworkSource.User)]
    public async Task ArtworkSources_ParseFromTheirWireNames(string artwork, WorkerArtworkSource source)
    {
        var body = $$"""{"jobId":"{{JobId}}","sourceKey":"upload:x","status":"queued","progress":0,"artwork":{{artwork}}}""";
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body));

        (await client.GetJobAsync(JobId, Ct))!.Artwork!.Source.ShouldBe(source);
    }

    [Fact]
    public async Task CreateMetadataJob_PostsTheRequestAndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "metadata-queued-job"));
        var request = new WorkerMetadataJobRequest
        {
            Title = "Hero of the Day",
            Artist = "Metallica",
            Album = "Load",
            DurationSeconds = 262.3,
            Isrc = "USEE19600011",
            StandardJobId = JobId,
        };

        var job = await client.CreateMetadataJobAsync(request, Ct);

        (job.MetadataJobId, job.Status, job.Stage).ShouldBe((MetadataJobId, WorkerMetadataJobState.Queued, "Queued"));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/metadata-jobs"));
        using var body = JsonDocument.Parse(sent.BodyText);
        body.RootElement.GetProperty("title").GetString().ShouldBe("Hero of the Day");
        body.RootElement.GetProperty("durationSeconds").GetDouble().ShouldBe(262.3);
        body.RootElement.GetProperty("isrc").GetString().ShouldBe("USEE19600011");
        body.RootElement.GetProperty("standardJobId").GetString().ShouldBe(JobId);
        body.RootElement.EnumerateObject().Select(p => p.Name).ShouldBe(["title", "artist", "album", "durationSeconds", "isrc", "standardJobId"], ignoreOrder: true);
    }

    [Fact]
    public async Task CreateMetadataJob_422BecomesARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.UnprocessableEntity, "problem-422-validation"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateMetadataJobAsync(new WorkerMetadataJobRequest { Title = "x" }, Ct));

        (error.StatusCode, error.Detail).ShouldBe((HttpStatusCode.UnprocessableEntity, "The request body is not valid."));
    }

    [Theory]
    [InlineData("metadata-running-job", WorkerMetadataJobState.Running, "Searching MusicBrainz", null)]
    [InlineData("metadata-completed-job", WorkerMetadataJobState.Completed, "Completed", null)]
    [InlineData("metadata-failed-job", WorkerMetadataJobState.Failed, "Failed", "Band information could not be retrieved; try again later.")]
    public async Task GetMetadataJob_ParsesEveryState(string example, WorkerMetadataJobState state, string stage, string? error)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var job = (await client.GetMetadataJobAsync(MetadataJobId, Ct))!;

        (job.Status, job.Stage, job.Error).ShouldBe((state, stage, error));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/metadata-jobs/{MetadataJobId}");
    }

    [Fact]
    public async Task GetMetadataJob_404IsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetMetadataJobAsync(MetadataJobId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetMetadataResult_ParsesTheFoundResult()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "metadata-result-found"));

        var result = await client.GetMetadataResultAsync(MetadataJobId, Ct);

        result.Outcome.ShouldBe(WorkerMetadataOutcome.Found);
        (result.Recording!.Mbid, result.Recording.Title, result.Recording.MatchedBy).ShouldBe((Guid.Parse("0f4e8a7c-3b2d-4c1e-9f8a-7b6c5d4e3f2a"), "Example Song", "isrc"));
        (result.ReleaseGroup!.Title, result.ReleaseGroup.FirstReleaseYear).ShouldBe(("Example Album", 1996));
        result.Artwork!.Source.ShouldBe(WorkerArtworkSource.CoverArtArchive);
        var artist = result.Artist!;
        (artist.Name, artist.Type, artist.Country, artist.BeginYear, artist.EndYear).ShouldBe(("The Example Band", WorkerArtistType.Group, "US", 1981, null));
        artist.Genres.ShouldBe(["heavy metal", "thrash metal"]);
        artist.Members[1].ShouldBe(new WorkerArtistMember("Second Member", 1983, 2001));
        (artist.Website, artist.WikidataId).ShouldBe(("https://www.example.org/", "Q15920"));
        (artist.Summary!.Language, artist.Summary.RevisionId).ShouldBe(("en", "1255000000"));
        artist.FetchedAt.ShouldBe(new DateTimeOffset(2026, 11, 15, 12, 0, 14, TimeSpan.Zero));
        result.Warnings.ShouldBeEmpty();
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/metadata-jobs/{MetadataJobId}/result");
    }

    [Theory]
    [InlineData("metadata-result-not-found", WorkerMetadataOutcome.NotFound)]
    [InlineData("metadata-result-disabled", WorkerMetadataOutcome.Disabled)]
    public async Task GetMetadataResult_ParsesOutcomesWithoutFacts(string example, WorkerMetadataOutcome outcome)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var result = await client.GetMetadataResultAsync(MetadataJobId, Ct);

        (result.Outcome, result.Recording, result.Artist, result.Artwork).ShouldBe((outcome, null, null, null));
    }

    [Fact]
    public async Task GetMetadataResult_409BecomesARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409"));

        (await Should.ThrowAsync<WorkerRequestException>(() => client.GetMetadataResultAsync(MetadataJobId, Ct))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(WorkerArtworkOwner.Job, JobId, "cover", "/v1/jobs/3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c/artwork/cover")]
    [InlineData(WorkerArtworkOwner.MetadataJob, MetadataJobId, "thumb", "/v1/metadata-jobs/5d2c7e9f1a3b4c6d8e0f2a4b6c8d0e1f/artwork/thumb")]
    [InlineData(WorkerArtworkOwner.Upload, ArtworkId, "cover", "/v1/artwork/8e4f2a6c0b1d4e3f9a7c5b3d1e0f2a4c/cover")]
    public async Task GetArtwork_DownloadsTheJpeg(WorkerArtworkOwner owner, string id, string variant, string path)
    {
        var (client, handler) = Create(_ => Jpeg([0xFF, 0xD8, 0xFF, 0xD9]));

        var bytes = await client.GetArtworkAsync(new WorkerArtworkRef(owner, id, variant), Ct);

        bytes.ShouldBe(new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe(path);
    }

    [Fact]
    public async Task GetArtwork_404IsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetArtworkAsync(new WorkerArtworkRef(WorkerArtworkOwner.Job, JobId, "cover"), Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetArtwork_AnOversizedBodyIsAnInvalidResponse()
    {
        var (client, _) = Create(_ => Jpeg(new byte[WorkerClient.MaxArtworkBytes + 1]));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetArtworkAsync(new WorkerArtworkRef(WorkerArtworkOwner.Job, JobId, "cover"), Ct));
    }

    [Theory]
    [InlineData(WorkerArtworkOwner.Job, "../x", "cover")]
    [InlineData(WorkerArtworkOwner.Job, JobId, "large")]
    [InlineData(WorkerArtworkOwner.Upload, ArtworkId, "../cover")]
    [InlineData((WorkerArtworkOwner)7, JobId, "cover")]
    public async Task GetArtwork_RejectsInvalidReferencesWithoutARequest(WorkerArtworkOwner owner, string id, string variant)
    {
        var (client, handler) = Create(_ => Jpeg([1]));

        await Should.ThrowAsync<ArgumentException>(() => client.GetArtworkAsync(new WorkerArtworkRef(owner, id, variant), Ct));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task NormalizeArtwork_StreamsTheFilePartAndParsesTheUpload()
    {
        var parts = new List<HttpContent>();
        var (client, handler) = Create(request =>
        {
            parts.AddRange(request.Content.ShouldBeOfType<MultipartFormDataContent>());
            return StubHttpMessageHandler.Json(HttpStatusCode.Created, WorkerExamples.Read("artwork-upload"));
        });
        using var content = new MemoryStream([9, 8, 7]);

        var upload = await client.NormalizeArtworkAsync(new WorkerArtworkFile(content, "me.png", "image/png"), Ct);

        upload.ArtworkId.ShouldBe(ArtworkId);
        (upload.Info.Source, upload.Info.CoverBytes, upload.Info.OriginUrl).ShouldBe((WorkerArtworkSource.User, 61234, null));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/artwork"));
        var part = parts.Single().ShouldBeOfType<StreamContent>();
        part.Headers.ContentDisposition!.Name!.Trim('"').ShouldBe("file");
        part.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe("me.png");
        part.Headers.ContentType!.MediaType.ShouldBe("image/png");
        sent.Body!.TakeLast(20).ShouldContain((byte)7);
    }

    [Fact]
    public async Task NormalizeArtwork_WithoutMediaTypeSendsOctetStream()
    {
        MediaTypeHeaderValue? type = null;
        var (client, _) = Create(request =>
        {
            type = request.Content.ShouldBeOfType<MultipartFormDataContent>().Single().Headers.ContentType;
            return StubHttpMessageHandler.Json(HttpStatusCode.Created, WorkerExamples.Read("artwork-upload"));
        });
        using var content = new MemoryStream([1]);

        await client.NormalizeArtworkAsync(new WorkerArtworkFile(content, "x", null), Ct);

        type!.MediaType.ShouldBe("application/octet-stream");
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(HttpStatusCode.UnsupportedMediaType)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task NormalizeArtwork_RejectionsKeepStatusAndDetail(HttpStatusCode status)
    {
        var problem = $$"""{"title":"Picture problem","status":{{(int)status}},"detail":"The file could not be read as a picture."}""";
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(status, problem, "application/problem+json"));
        using var content = new MemoryStream([1]);

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.NormalizeArtworkAsync(new WorkerArtworkFile(content, "x.jpg", "image/jpeg"), Ct));

        (error.StatusCode, error.Detail).ShouldBe((status, "The file could not be read as a picture."));
    }

    [Fact]
    public async Task UnreachableWorker_ThrowsUnavailableForEveryNewCall()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));
        using var content = new MemoryStream([1]);

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateMetadataJobAsync(new WorkerMetadataJobRequest { Title = "x" }, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetMetadataJobAsync(MetadataJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetMetadataResultAsync(MetadataJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetArtworkAsync(new WorkerArtworkRef(WorkerArtworkOwner.Job, JobId, "thumb"), Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.NormalizeArtworkAsync(new WorkerArtworkFile(content, "x.jpg", "image/jpeg"), Ct));
    }

    [Fact]
    public async Task InvalidMetadataJobIds_AreRejectedWithoutARequest()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.GetMetadataJobAsync("../x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetMetadataResultAsync("ABC", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    private static HttpResponseMessage Jpeg(byte[] bytes)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        response.Content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return response;
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
