using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

public sealed class WorkerClientTests : IDisposable
{
    private const string JobId = WorkerExamples.JobId;
    private readonly string _tempDir = Directory.CreateTempSubdirectory("stemstudio-worker-").FullName;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_tempDir, true);

    [Theory]
    [InlineData(HttpStatusCode.Accepted, "queued-job", WorkerJobState.Queued)]
    [InlineData(HttpStatusCode.OK, "completed-job", WorkerJobState.Completed)]
    public async Task CreateUrlJob_PostsJsonAndParsesJob(HttpStatusCode status, string example, WorkerJobState state)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var job = await client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), Ct);

        job.JobId.ShouldBe(JobId);
        job.Status.ShouldBe(state);
        job.SourceKey.ShouldBe("youtube:dQw4w9WgXcQ");
        var request = handler.Requests.Single();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri.AbsolutePath.ShouldBe("/v1/jobs");
        using var body = JsonDocument.Parse(request.BodyText);
        body.RootElement.GetProperty("sourceType").GetString().ShouldBe("youtube");
        body.RootElement.GetProperty("url").GetString().ShouldBe("https://www.youtube.com/watch?v=dQw4w9WgXcQ");
        body.RootElement.EnumerateObject().Count().ShouldBe(2);
    }

    [Fact]
    public async Task CreateUrlJob_SendsSpotifySourceType()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "queued-job"));

        await client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Spotify, "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT"), Ct);

        handler.Requests.Single().BodyText.ShouldContain("\"sourceType\":\"spotify\"");
    }

    [Fact]
    public async Task CreateUrlJob_RejectsUploadSourceType()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "queued-job"));

        await Should.ThrowAsync<ArgumentException>(() => client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Upload, "x"), Ct));
    }

    [Fact]
    public async Task CreateUploadJob_StreamsFilePartFromDisk()
    {
        var path = Path.Combine(_tempDir, "abc.flac");
        await File.WriteAllBytesAsync(path, [1, 2, 3, 4], Ct);
        var parts = new List<HttpContent>();
        var (client, handler) = Create(request =>
        {
            parts.AddRange(request.Content.ShouldBeOfType<MultipartFormDataContent>());
            return StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "queued-job");
        });

        var job = await client.CreateUploadJobAsync(new WorkerUpload(path, "abc.flac"), Ct);

        job.Status.ShouldBe(WorkerJobState.Queued);
        var request = handler.Requests.Single();
        request.Uri.AbsolutePath.ShouldBe("/v1/jobs");
        var part = parts.Single().ShouldBeOfType<StreamContent>();
        part.Headers.ContentDisposition!.Name!.Trim('"').ShouldBe("file");
        part.Headers.ContentDisposition.FileName!.Trim('"').ShouldBe("abc.flac");
        part.Headers.ContentType!.MediaType.ShouldBe("application/octet-stream");
        request.Body.ShouldNotBeNull();
        request.BodyText.ShouldContain("\u0001\u0002\u0003\u0004");
    }

    [Theory]
    [InlineData("separating-job", WorkerJobState.Separating, 47)]
    [InlineData("failed-job", WorkerJobState.Failed, 5)]
    public async Task GetJob_ParsesStatusProgressAndMetadata(string example, WorkerJobState state, int progress)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var job = await client.GetJobAsync(JobId, Ct);

        job.ShouldNotBeNull();
        job.Status.ShouldBe(state);
        job.Progress.ShouldBe(progress);
        job.Metadata.ShouldNotBeNull();
        job.Metadata.Title.ShouldNotBeNullOrEmpty();
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/jobs/{JobId}");
    }

    [Fact]
    public async Task GetJob_FailedJobCarriesError()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "failed-job"));

        var job = await client.GetJobAsync(JobId, Ct);

        job!.Error.ShouldBe("No matching YouTube audio was found for this Spotify track.");
        job.Metadata!.SourceUrl.ShouldBeNull();
    }

    [Fact]
    public async Task GetJob_NotFoundReturnsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetJobAsync(JobId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task GetJob_RejectsMalformedJobId()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "queued-job"));

        await Should.ThrowAsync<ArgumentException>(() => client.GetJobAsync("../../etc", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetJobAsync(JobId + "\n", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetResult_ParsesSixStemsAndBeatGrid()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "job-result"));

        var result = await client.GetResultAsync(JobId, Ct);

        result.Model.ShouldBe("htdemucs_6s");
        result.DurationSeconds.ShouldBe(213.04);
        result.Metadata.Title.ShouldBe("Never Gonna Give You Up");
        result.Metadata.Artist.ShouldBe("Rick Astley");
        result.Stems.Select(s => s.Name).ShouldBe(StemCatalog.SixStem);
        result.Stems[0].SizeBytes.ShouldBe(21456789);
        result.BeatGrid.Bpm.ShouldBe(113.5);
        result.BeatGrid.Beats.ShouldBe([0.52, 1.05, 1.58, 2.11]);
        result.BeatGrid.Downbeats.ShouldBe([0.52]);
        result.BeatGrid.BeatsPerBar.ShouldBe(4);
        result.BeatGrid.Confidence.ShouldBe(0.87);
        result.BeatGrid.Reliable.ShouldBeTrue();
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/jobs/{JobId}/result");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "problem-404", "does not exist")]
    [InlineData(HttpStatusCode.Conflict, "problem-409", "separating")]
    public async Task GetResult_ProblemThrowsRequestException(HttpStatusCode status, string example, string detail)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.GetResultAsync(JobId, Ct));

        error.StatusCode.ShouldBe(status);
        error.Detail.ShouldContain(detail);
    }

    [Fact]
    public async Task DownloadStem_CopiesTheFlacStream()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9, 8, 7]) });
        using var destination = new MemoryStream();

        await client.DownloadStemAsync(JobId, "guitar", destination, Ct);

        destination.ToArray().ShouldBe([9, 8, 7]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/jobs/{JobId}/stems/guitar");
    }

    [Fact]
    public async Task DownloadStem_RejectsUnknownStemName()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.DownloadStemAsync(JobId, "../x", Stream.Null, Ct));
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task DownloadStem_ProblemThrowsRequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.DownloadStemAsync(JobId, "bass", Stream.Null, Ct));

        error.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400", "Only YouTube video and Spotify track URLs are supported")]
    [InlineData(HttpStatusCode.RequestEntityTooLarge, "problem-413", "larger than 200 MiB")]
    public async Task CreateJob_ClientErrorsThrowRequestExceptionWithDetail(HttpStatusCode status, string example, string detail)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var error = await Should.ThrowAsync<WorkerRequestException>(
            () => client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), Ct));

        error.StatusCode.ShouldBe(status);
        error.Detail.ShouldContain(detail);
    }

    [Theory]
    [InlineData("", "Bad Request")]
    [InlineData("{\"title\":\"Invalid source\",\"status\":400}", "Invalid source")]
    [InlineData("not json", "Bad Request")]
    public async Task CreateJob_ProblemWithoutDetailFallsBack(string body, string expected)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.BadRequest, body, "application/problem+json"));

        var error = await Should.ThrowAsync<WorkerRequestException>(
            () => client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), Ct));

        error.Detail.ShouldBe(expected);
    }

    [Fact]
    public async Task CreateJob_ProblemWithoutReasonPhraseUsesStatusName()
    {
        var (client, _) = Create(_ =>
        {
            var response = StubHttpMessageHandler.Json(HttpStatusCode.UnsupportedMediaType, "{}", "application/problem+json");
            response.ReasonPhrase = null;
            return response;
        });

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateUploadJobAsync(new WorkerUpload(WriteTempFile(), "a.mp3"), Ct));

        error.Detail.ShouldNotBeNullOrWhiteSpace();
        error.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public void Registration_AppendsSlashToBasePath()
    {
        var stub = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["StemStudio:WorkerBaseUrl"] = "http://host/worker" })
            .Build();
        var services = new ServiceCollection();
        services.AddWorkerClient(configuration).ConfigurePrimaryHttpMessageHandler(() => stub);
        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IHttpClientFactory>();

        factory.CreateClient(nameof(IWorkerClient)).BaseAddress.ShouldBe(new Uri("http://host/worker/"));
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ServerErrorsThrowUnavailable(HttpStatusCode status)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(status));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetJobAsync(JobId, Ct));
    }

    [Fact]
    public async Task ConnectionRefusedThrowsUnavailable()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("Connection refused", new System.Net.Sockets.SocketException(10061)));

        var error = await Should.ThrowAsync<WorkerUnavailableException>(
            () => client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), Ct));

        error.InnerException.ShouldBeOfType<HttpRequestException>();
    }

    public static TheoryData<Exception> ResilienceFailures() =>
    [
        new Polly.Timeout.TimeoutRejectedException(),
        new Polly.CircuitBreaker.BrokenCircuitException(),
    ];

    [Theory]
    [MemberData(nameof(ResilienceFailures))]
    public async Task ResiliencePipelineFailuresThrowUnavailable(Exception failure)
    {
        var (client, _) = Create(_ => throw failure);

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetJobAsync(JobId, Ct));
    }

    [Fact]
    public async Task NullProblemBodyFallsBackToReasonPhrase()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.Conflict, "null", "application/problem+json"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.GetResultAsync(JobId, Ct));

        error.Detail.ShouldBe("Conflict");
    }

    [Fact]
    public async Task TimeoutThrowsUnavailable()
    {
        var (client, _) = Create(_ => throw new TaskCanceledException("timeout"));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetJobAsync(JobId, Ct));
    }

    [Fact]
    public async Task CallerCancellationIsNotUnavailable()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var (client, _) = Create(_ => throw new TaskCanceledException("cancelled"));

        await Should.ThrowAsync<OperationCanceledException>(() => client.GetJobAsync(JobId, cancelled.Token));
    }

    [Fact]
    public async Task InvalidJsonThrowsUnavailable()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, "null"));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetJobAsync(JobId, Ct));
    }

    [Fact]
    public async Task Registration_RetriesGetOn503ButNeverPost()
    {
        var calls = 0;
        var stub = new StubHttpMessageHandler(_ => Task.FromResult(Interlocked.Increment(ref calls) < 3
            ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            : StubHttpMessageHandler.Example(HttpStatusCode.OK, "separating-job")));
        using var provider = RegisteredProvider(stub);
        var client = provider.GetRequiredService<IWorkerClient>();

        (await client.GetJobAsync(JobId, Ct))!.Status.ShouldBe(WorkerJobState.Separating);
        calls.ShouldBe(3);

        calls = -10;
        await Should.ThrowAsync<WorkerUnavailableException>(
            () => client.CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Youtube, "https://www.youtube.com/watch?v=dQw4w9WgXcQ"), Ct));
        calls.ShouldBe(-9);
    }

    [Fact]
    public async Task Registration_CreateJobWaitsLongerThanThePollTimeout()
    {
        using var stub = new SlowHandler(TimeSpan.FromMilliseconds(400), HttpStatusCode.Accepted, "queued-job");
        using var provider = RegisteredProvider(stub, new() { ["StemStudio:WorkerPollTimeout"] = "00:00:00.05", ["StemStudio:WorkerCreateJobTimeout"] = "00:00:30" });

        var job = await provider.GetRequiredService<IWorkerClient>()
            .CreateUrlJobAsync(new WorkerUrlJobRequest(SourceType.Spotify, "https://open.spotify.com/track/4cOdK2wGLETKBW3PvgPWqT"), Ct);

        job.Status.ShouldBe(WorkerJobState.Queued);
    }

    [Fact]
    public async Task Registration_StatusPollGivesUpAfterThePollTimeout()
    {
        using var stub = new SlowHandler(TimeSpan.FromSeconds(30), HttpStatusCode.OK, "separating-job");
        using var provider = RegisteredProvider(stub, new() { ["StemStudio:WorkerPollTimeout"] = "00:00:00.05", ["StemStudio:WorkerCreateJobTimeout"] = "00:00:30" });
        var started = System.Diagnostics.Stopwatch.StartNew();

        await Should.ThrowAsync<WorkerUnavailableException>(() => provider.GetRequiredService<IWorkerClient>().GetJobAsync(JobId, Ct));

        started.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(20));
    }

    [Fact]
    public void Timeouts_PostGetsTheCreateJobTimeoutEverythingElseThePollTimeout()
    {
        var options = new StemStudioApiOptions();
        using var post = new HttpRequestMessage(HttpMethod.Post, "v1/jobs");
        using var get = new HttpRequestMessage(HttpMethod.Get, "v1/jobs");

        options.WorkerCreateJobTimeout.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(60));
        options.WorkerPollTimeout.ShouldBeLessThan(options.WorkerCreateJobTimeout);
        options.OrphanedImportTimeout.ShouldBeGreaterThan(options.WorkerCreateJobTimeout);
        WorkerTimeouts.For(post, options).ShouldBe(options.WorkerCreateJobTimeout);
        WorkerTimeouts.For(get, options).ShouldBe(options.WorkerPollTimeout);
        WorkerTimeouts.For(null, options).ShouldBe(options.WorkerPollTimeout);
    }

    [Fact]
    public void Registration_UsesConfiguredBaseAddress()
    {
        var stub = new StubHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var provider = RegisteredProvider(stub);

        provider.GetRequiredService<IWorkerClient>().ShouldBeOfType<WorkerClient>();
    }

    [Fact]
    public void Exceptions_HaveStandardConstructors()
    {
        new WorkerUnavailableException().Message.ShouldNotBeNullOrEmpty();
        new WorkerUnavailableException("x").Message.ShouldBe("x");
        new WorkerRequestException().StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        new WorkerRequestException("x").Detail.ShouldBe("x");
        new WorkerRequestException("x", new InvalidOperationException()).InnerException.ShouldNotBeNull();
    }

    private string WriteTempFile()
    {
        var path = Path.Combine(_tempDir, $"{Guid.NewGuid():N}.mp3");
        File.WriteAllBytes(path, [1]);
        return path;
    }

    internal static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpMessageHandler(request => Task.FromResult(respond(request)));
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://worker:8000") };
        return (new WorkerClient(http, Microsoft.Extensions.Logging.Abstractions.NullLogger<WorkerClient>.Instance, TimeProvider.System), handler);
    }

    private static ServiceProvider RegisteredProvider(HttpMessageHandler stub, Dictionary<string, string?>? settings = null)
    {
        var values = new Dictionary<string, string?>(settings ?? []) { ["StemStudio:WorkerBaseUrl"] = "http://worker:8000" };
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddWorkerClient(configuration).ConfigurePrimaryHttpMessageHandler(() => stub);
        return services.BuildServiceProvider();
    }

    /// <summary>Answers after a delay, honouring the cancellation of the resilience timeout.</summary>
    private sealed class SlowHandler(TimeSpan delay, HttpStatusCode status, string example) : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(delay, cancellationToken);
            return StubHttpMessageHandler.Example(status, example);
        }
    }
}
