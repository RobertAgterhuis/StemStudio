using System.Net;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 007 T031: render job calls of the worker client against the worker 1.4.0 examples.</summary>
public sealed class WorkerRenderClientTests
{
    private const string RenderJobId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static WorkerRenderJobRequest RawRequest() => new()
    {
        Inputs = [new WorkerRenderInput("tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/stems/drums.flac", 1, 0)],
        MasterGain = 1,
        Output = new WorkerRenderOutput(WorkerRenderFormat.Flac) { BitDepth = 16, Dither = false },
        Metadata = new WorkerRenderMetadata("Song", "Band", "drums, raw"),
    };

    [Fact]
    public async Task Create_PostsTheRequestAndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "render-queued-job"));

        var job = await client.CreateRenderJobAsync(RawRequest(), Ct);

        (job.RenderJobId, job.Status, job.Progress, job.Result).ShouldBe((RenderJobId, WorkerRenderJobState.Queued, 0, null));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/render-jobs"));
        using var body = JsonDocument.Parse(sent.BodyText);
        var root = body.RootElement;
        root.GetProperty("inputs")[0].GetProperty("path").GetString().ShouldEndWith("/stems/drums.flac");
        root.GetProperty("output").GetProperty("format").GetString().ShouldBe("flac");
        root.GetProperty("output").GetProperty("bitDepth").GetInt32().ShouldBe(16);
        root.GetProperty("output").GetProperty("dither").GetBoolean().ShouldBeFalse();
        root.GetProperty("output").TryGetProperty("mp3BitrateKbps", out _).ShouldBeFalse();
        root.GetProperty("range").ValueKind.ShouldBe(JsonValueKind.Null);
        root.GetProperty("metadata").GetProperty("comment").GetString().ShouldBe("drums, raw");
    }

    [Fact]
    public async Task Create_SendsARangeAndAnMp3BitRate()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "render-queued-job"));
        var request = RawRequest() with { Range = new WorkerRenderRange(30, 75), Output = new WorkerRenderOutput(WorkerRenderFormat.Mp3) { Mp3BitrateKbps = 320 } };

        await client.CreateRenderJobAsync(request, Ct);

        using var body = JsonDocument.Parse(handler.Requests.Single().BodyText);
        body.RootElement.GetProperty("range").GetProperty("endSeconds").GetDouble().ShouldBe(75);
        body.RootElement.GetProperty("output").GetProperty("mp3BitrateKbps").GetInt32().ShouldBe(320);
        body.RootElement.GetProperty("output").TryGetProperty("bitDepth", out _).ShouldBeFalse();
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400-input-path", "Input path is not allowed: tracks/../stemstudio.db")]
    [InlineData(HttpStatusCode.NotFound, "problem-404-input", "Input not found: tracks/0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a/detail/drums/kick.flac")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-validation", "The request body is not valid.")]
    public async Task Create_ProblemsKeepStatusAndDetail(HttpStatusCode status, string example, string detail)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateRenderJobAsync(RawRequest(), Ct));

        (error.StatusCode, error.Detail).ShouldBe((status, detail));
    }

    [Theory]
    [InlineData("render-queued-job", WorkerRenderJobState.Queued, 0)]
    [InlineData("render-running-job", WorkerRenderJobState.Running, 0.62)]
    [InlineData("render-failed-job", WorkerRenderJobState.Failed, 0.2)]
    [InlineData("render-completed-job", WorkerRenderJobState.Completed, 1)]
    public async Task Get_ParsesEveryStatus(string example, WorkerRenderJobState state, double progress)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var job = (await client.GetRenderJobAsync(RenderJobId, Ct))!;

        (job.Status, job.Progress).ShouldBe((state, progress));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/render-jobs/{RenderJobId}");
    }

    [Fact]
    public async Task Get_ParsesTheResultAndTheError()
    {
        var (completed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "render-completed-job"));
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "render-failed-job"));

        var result = (await completed.GetRenderJobAsync(RenderJobId, Ct))!.Result!;
        var error = (await failed.GetRenderJobAsync(RenderJobId, Ct))!.Error;

        (result.FileName, result.ContentType, result.SizeBytes, result.DurationSeconds).ShouldBe(("output.mp3", "audio/mpeg", 1824512L, 45.5));
        (result.Sha256.Length, result.SampleRate, result.Channels, result.PeakDbfs, result.AppliedGainDb).ShouldBe((64, 44100, 2, -0.1, -1.8));
        error.ShouldBe("interrupted by restart");
    }

    [Fact]
    public async Task Get_404IsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetRenderJobAsync(RenderJobId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task DownloadFile_StreamsTheBody()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var destination = new MemoryStream();

        await client.DownloadRenderFileAsync(RenderJobId, destination, Ct);

        destination.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/render-jobs/{RenderJobId}/file");
    }

    [Fact]
    public async Task DownloadFile_409IsARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-render"));
        using var destination = new MemoryStream();

        (await Should.ThrowAsync<WorkerRequestException>(() => client.DownloadRenderFileAsync(RenderJobId, destination, Ct))).Detail.ShouldBe("Render job is in state running.");
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Delete_Is204AndTolerates404(HttpStatusCode status)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(status));

        await client.DeleteRenderJobAsync(RenderJobId, Ct);

        (handler.Requests.Single().Method, handler.Requests.Single().Uri.AbsolutePath).ShouldBe((HttpMethod.Delete, $"/v1/render-jobs/{RenderJobId}"));
    }

    [Fact]
    public async Task Delete_OtherRejectionsThrow()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409"));

        await Should.ThrowAsync<WorkerRequestException>(() => client.DeleteRenderJobAsync(RenderJobId, Ct));
    }

    [Fact]
    public async Task UnreachableWorker_ThrowsUnavailableForEveryRenderCall()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));
        using var destination = new MemoryStream();

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateRenderJobAsync(RawRequest(), Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetRenderJobAsync(RenderJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.DownloadRenderFileAsync(RenderJobId, destination, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.DeleteRenderJobAsync(RenderJobId, Ct));
    }

    [Fact]
    public async Task InvalidJobIds_AreRejectedWithoutARequest()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.GetRenderJobAsync("../x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DeleteRenderJobAsync("ABC", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
