using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Detail job methods of the worker client against the worker 1.1.0 examples (T030).</summary>
public sealed class WorkerDetailClientTests
{
    private const string DetailJobId = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";
    private const string StandardJobId = WorkerExamples.JobId;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("detail-queued-job", "QueuedDetailJob")]
    [InlineData("detail-running-job", "RunningDetailJob")]
    [InlineData("detail-failed-job", "FailedDetailJob")]
    [InlineData("detail-completed-job", "CompletedDetailJob")]
    [InlineData("detail-result", "DetailResultExample")]
    public void Examples_AreCopiesOfTheWorkerContractExamples(string file, string example)
    {
        var contract = OpenApiContract.Load(Path.Combine(AppContext.BaseDirectory, "Contract", "worker-api.openapi.yaml"));
        var expected = contract.Root["components"]!["examples"]![example]!["value"]!;

        JsonNode.DeepEquals(JsonNode.Parse(WorkerExamples.Read(file)), expected).ShouldBeTrue(file);
    }

    [Theory]
    [InlineData(HttpStatusCode.Accepted, "detail-queued-job", WorkerDetailJobState.Queued)]
    [InlineData(HttpStatusCode.OK, "detail-completed-job", WorkerDetailJobState.Completed)]
    public async Task CreateDetailJob_PostsTheStandardJobAndParsesTheJob(HttpStatusCode status, string example, WorkerDetailJobState state)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var job = await client.CreateDetailJobAsync(StandardJobId, Ct);

        (job.DetailJobId, job.StandardJobId, job.Status).ShouldBe((DetailJobId, StandardJobId, state));
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/detail-jobs"));
        using var body = JsonDocument.Parse(request.BodyText);
        body.RootElement.GetProperty("standardJobId").GetString().ShouldBe(StandardJobId);
        body.RootElement.EnumerateObject().Count().ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "problem-404")]
    [InlineData(HttpStatusCode.Conflict, "problem-409-detail")]
    public async Task CreateDetailJob_RejectionsBecomeRequestExceptions(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateDetailJobAsync(StandardJobId, Ct));

        error.StatusCode.ShouldBe(status);
        error.Detail.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetDetailJob_ParsesRunningAndFailedJobs()
    {
        var (running, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-running-job"));
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-failed-job"));

        var job = (await running.GetDetailJobAsync(DetailJobId, Ct))!;
        var failure = (await failed.GetDetailJobAsync(DetailJobId, Ct))!;

        (job.Status, job.Stage, job.CurrentParent, job.Progress).ShouldBe((WorkerDetailJobState.Running, "Splitting drums (1 of 3)", "drums", 12));
        (failure.Status, failure.Error).ShouldBe((WorkerDetailJobState.Failed, "Detail model could not be downloaded; try again when online."));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/detail-jobs/{DetailJobId}");
    }

    [Fact]
    public async Task GetDetailJob_404IsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetDetailJobAsync(DetailJobId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task CancelDetailJob_PostsAndParsesTheCancelledJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-cancelled-job"));

        var job = await client.CancelDetailJobAsync(DetailJobId, Ct);

        job.Status.ShouldBe(WorkerDetailJobState.Cancelled);
        var request = handler.Requests.Single();
        (request.Method, request.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, $"/v1/detail-jobs/{DetailJobId}/cancel"));
    }

    [Fact]
    public async Task CancelDetailJob_409IsARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-detail"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CancelDetailJobAsync(DetailJobId, Ct));

        (error.StatusCode, error.Detail).ShouldBe((HttpStatusCode.Conflict, "Detail job is in state running."));
    }

    [Fact]
    public async Task GetDetailResult_ParsesOutcomesAndStems()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "detail-result"));

        var result = await client.GetDetailResultAsync(DetailJobId, Ct);

        result.DetailJobId.ShouldBe(DetailJobId);
        result.Parents.Select(p => p.Parent).ShouldBe(["vocals", "drums", "bass", "guitar", "piano", "other"]);
        result.Parents.Select(p => p.Outcome).ShouldBe(
        [
            WorkerParentOutcomeKind.Split, WorkerParentOutcomeKind.Split, WorkerParentOutcomeKind.NotSupported,
            WorkerParentOutcomeKind.NothingFurther, WorkerParentOutcomeKind.SkippedSilent, WorkerParentOutcomeKind.SkippedSilent,
        ]);
        result.Parents[1].Stems.Select(s => s.Name).ShouldBe(["kick", "snare", "toms", "hihat", "cymbals", "rest"]);
        result.Parents[0].Stems[0].ShouldBe(new WorkerDetailStemInfo("lead", $"/v1/detail-jobs/{DetailJobId}/stems/vocals/lead", 19456789));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/detail-jobs/{DetailJobId}/result");
    }

    [Fact]
    public async Task GetDetailResult_409IsARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-detail"));

        await Should.ThrowAsync<WorkerRequestException>(() => client.GetDetailResultAsync(DetailJobId, Ct));
    }

    [Fact]
    public async Task DownloadDetailStem_StreamsTheFlac()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) });
        using var destination = new MemoryStream();

        await client.DownloadDetailStemAsync(new WorkerDetailStemRef(DetailJobId, "drums", "kick"), destination, Ct);

        destination.ToArray().ShouldBe([1, 2, 3]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/detail-jobs/{DetailJobId}/stems/drums/kick");
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "problem-404")]
    [InlineData(HttpStatusCode.Conflict, "problem-409-detail")]
    public async Task DownloadDetailStem_RejectionsBecomeRequestExceptions(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));
        using var destination = new MemoryStream();

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.DownloadDetailStemAsync(new WorkerDetailStemRef(DetailJobId, "drums", "kick"), destination, Ct));

        error.StatusCode.ShouldBe(status);
    }

    [Theory]
    [InlineData("bass", "rest")]
    [InlineData("vocals", "kick")]
    [InlineData("drums", "../kick")]
    [InlineData("..", "rest")]
    public async Task DownloadDetailStem_RejectsNamesOutsideTheEnumsWithoutARequest(string parent, string name)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var destination = new MemoryStream();

        await Should.ThrowAsync<ArgumentException>(() => client.DownloadDetailStemAsync(new WorkerDetailStemRef(DetailJobId, parent, name), destination, Ct));

        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidJobIds_AreRejectedWithoutARequest()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.CreateDetailJobAsync("../x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetDetailJobAsync("ABC", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.CancelDetailJobAsync("1", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnreachableWorker_ThrowsUnavailableForEveryDetailCall()
    {
        var (client, _) = Create(_ => throw new HttpRequestException("connection refused"));
        using var destination = new MemoryStream();

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateDetailJobAsync(StandardJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetDetailJobAsync(DetailJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CancelDetailJobAsync(DetailJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetDetailResultAsync(DetailJobId, Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.DownloadDetailStemAsync(new WorkerDetailStemRef(DetailJobId, "drums", "kick"), destination, Ct));
    }

    [Fact]
    public async Task ServerErrors_ThrowUnavailable()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.GetDetailJobAsync(DetailJobId, Ct));
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
