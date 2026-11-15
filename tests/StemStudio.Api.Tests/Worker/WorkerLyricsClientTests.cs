using System.Net;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 006 T023: lyrics job calls of the worker client against the worker 1.5.0 examples (self-written text).</summary>
public sealed class WorkerLyricsClientTests
{
    private const string AlignJobId = "9a4c6e8b0d2f41a3b5c7d9e1f3a5b7c9";
    private const string LookupJobId = "5e8d2c4a6b1f40e9a7c3d5b1e9f20c48";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task CreateLookup_PostsTheExampleBodyAndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "lyrics-queued-lookup-job"));
        var request = new WorkerLyricsLookupRequest { Title = "Fake Song", Artist = "The Fixture Band", Album = "Fixture Album", DurationSeconds = 262.0, StandardJobId = WorkerExamples.JobId };

        var job = await client.CreateLyricsLookupJobAsync(request, Ct);

        (job.LyricsJobId, job.Mode, job.Status).ShouldBe((LookupJobId, WorkerLyricsJobMode.Lookup, WorkerLyricsJobState.Queued));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/lyrics-jobs"));
        JsonNode.DeepEquals(JsonNode.Parse(sent.BodyText), JsonNode.Parse(WorkerExamples.Read("lyrics-lookup-request"))).ShouldBeTrue(sent.BodyText);
    }

    [Fact]
    public async Task CreateAlign_PostsTheExampleBody()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "lyrics-queued-align-job"));
        var request = new WorkerLyricsAlignRequest { StandardJobId = WorkerExamples.JobId, Text = "Fake line one\nLa la la", Language = "en" };

        var job = await client.CreateLyricsAlignJobAsync(request, Ct);

        (job.Mode, job.Status).ShouldBe((WorkerLyricsJobMode.Align, WorkerLyricsJobState.Queued));
        var sent = handler.Requests.Single().BodyText;
        JsonNode.DeepEquals(JsonNode.Parse(sent), JsonNode.Parse(WorkerExamples.Read("lyrics-align-request"))).ShouldBeTrue(sent);
    }

    [Fact]
    public async Task CreateAlign_SendsALineSyncedDocumentToRefineInTheContractShape()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "lyrics-queued-align-job"));
        var line = await ReadResultAsync("lyrics-result-line");

        await client.CreateLyricsAlignJobAsync(new WorkerLyricsAlignRequest { StandardJobId = WorkerExamples.JobId, Text = line.PlainText, Refine = line }, Ct);

        var refine = JsonNode.Parse(handler.Requests.Single().BodyText)!["refine"]!;
        JsonNode.DeepEquals(refine, JsonNode.Parse(WorkerExamples.Read("lyrics-result-line"))!["lyrics"]).ShouldBeTrue(refine.ToJsonString());
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "problem-404")]
    [InlineData(HttpStatusCode.Conflict, "problem-409-lyrics")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-validation")]
    public async Task CreateAlign_RejectionsBecomeRequestExceptions(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateLyricsAlignJobAsync(new WorkerLyricsAlignRequest { StandardJobId = WorkerExamples.JobId, Text = "x" }, Ct));

        error.StatusCode.ShouldBe(status);
    }

    [Theory]
    [InlineData("lyrics-running-align-job", WorkerLyricsJobState.Running, 0.4, 30.0)]
    [InlineData("lyrics-completed-lookup-job", WorkerLyricsJobState.Completed, 1.0, null)]
    [InlineData("lyrics-failed-align-job", WorkerLyricsJobState.Failed, 0.05, 30.0)]
    [InlineData("lyrics-cancelled-align-job", WorkerLyricsJobState.Cancelled, 0.4, 420.0)]
    public async Task GetJob_ParsesEveryState(string example, WorkerLyricsJobState state, double progress, double? estimate)
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));
        var id = JsonNode.Parse(WorkerExamples.Read(example))!["lyricsJobId"]!.GetValue<string>();

        var job = (await client.GetLyricsJobAsync(id, Ct))!;

        (job.Status, job.Stage, job.Progress, job.EstimatedSeconds).ShouldBe((state, JsonNode.Parse(WorkerExamples.Read(example))!["stage"]!.GetValue<string>(), progress, estimate));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/lyrics-jobs/{id}");
    }

    [Fact]
    public async Task GetJob_ParsesTheDeviceAndTheError()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "lyrics-running-align-job"));
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "lyrics-failed-align-job"));

        var job = (await client.GetLyricsJobAsync(AlignJobId, Ct))!;
        var failure = (await failed.GetLyricsJobAsync(AlignJobId, Ct))!;

        job.Device.ShouldBe(new WorkerJobDevice(ProcessingDeviceKind.Gpu, "NVIDIA GeForce RTX 5070 Ti", false, null));
        failure.Error.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetJob_404IsNull()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetLyricsJobAsync(AlignJobId, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("lyrics-result-word", WorkerLyricsOutcome.Found, SyncLevel.Word, LyricsOrigin.Source)]
    [InlineData("lyrics-result-line", WorkerLyricsOutcome.Found, SyncLevel.Line, LyricsOrigin.Interpolated)]
    [InlineData("lyrics-result-plain", WorkerLyricsOutcome.Found, SyncLevel.Plain, LyricsOrigin.None)]
    [InlineData("lyrics-result-aligned", WorkerLyricsOutcome.Found, SyncLevel.Word, LyricsOrigin.Aligned)]
    [InlineData("lyrics-result-instrumental", WorkerLyricsOutcome.Instrumental, SyncLevel.Plain, LyricsOrigin.None)]
    public async Task GetResult_ParsesTheDocuments(string example, WorkerLyricsOutcome outcome, SyncLevel level, LyricsOrigin origin)
    {
        var (client, handler) = Create(_ => NoStore(StubHttpMessageHandler.Example(HttpStatusCode.OK, example)));

        var result = await client.GetLyricsResultAsync(LookupJobId, Ct);

        (result.Outcome, result.Lyrics!.SyncLevel, result.Lyrics.Origin).ShouldBe((outcome, level, origin));
        Should.NotThrow(() => LyricsDocumentRules.Validate(result.Lyrics));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/lyrics-jobs/{LookupJobId}/result");
    }

    [Fact]
    public async Task GetResult_ParsesWordsWithTimesOriginsAndConfidence()
    {
        var aligned = await ReadResultAsync("lyrics-result-aligned");

        var word = aligned.Lines[0].Words[0];
        (word.StartMs is > 0, word.EndMs > word.StartMs, word.Origin).ShouldBe((true, true, WordOrigin.Aligned));
        (aligned.Source, aligned.Language, aligned.Instrumental).ShouldBe((LyricsSource.User, "en", false));
    }

    [Theory]
    [InlineData("lyrics-result-not-found", WorkerLyricsOutcome.NotFound)]
    [InlineData("lyrics-result-disabled", WorkerLyricsOutcome.Disabled)]
    public async Task GetResult_OutcomesWithoutLyrics(string example, WorkerLyricsOutcome outcome)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var result = await client.GetLyricsResultAsync(LookupJobId, Ct);

        (result.Outcome, result.Lyrics).ShouldBe((outcome, null));
    }

    [Fact]
    public async Task GetResult_409BecomesARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-lyrics-result"));

        var error = await Should.ThrowAsync<WorkerRequestException>(() => client.GetLyricsResultAsync(LookupJobId, Ct));

        (error.StatusCode, error.Detail).ShouldBe((HttpStatusCode.Conflict, "Lyrics job is in state running."));
    }

    [Fact]
    public async Task Cancel_PostsAndParsesTheCancelledJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "lyrics-cancelled-align-job"));

        var job = await client.CancelLyricsJobAsync(AlignJobId, Ct);

        job.Status.ShouldBe(WorkerLyricsJobState.Cancelled);
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, $"/v1/lyrics-jobs/{AlignJobId}/cancel"));
    }

    [Fact]
    public async Task Cancel_409BecomesARequestException()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-lyrics"));

        await Should.ThrowAsync<WorkerRequestException>(() => client.CancelLyricsJobAsync(AlignJobId, Ct));
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Delete_RemovesTheJobAndToleratesAnUnknownOne(HttpStatusCode status)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(status));

        await client.DeleteLyricsJobAsync(AlignJobId, Ct);

        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Delete, $"/v1/lyrics-jobs/{AlignJobId}"));
    }

    [Fact]
    public async Task Delete_ServerErrorsAreUnavailable()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.DeleteLyricsJobAsync(AlignJobId, Ct));
    }

    [Fact]
    public async Task InvalidLyricsJobIds_AreRejectedWithoutARequest()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));

        await Should.ThrowAsync<ArgumentException>(() => client.GetLyricsJobAsync("../x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetLyricsResultAsync("ABC", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.CancelLyricsJobAsync("x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DeleteLyricsJobAsync("x", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("gpu")]
    [InlineData("cpu")]
    public async Task Health_ParsesTheLyricsAlignment(string example)
    {
        var body = ContractJson.WorkerContract.Root["paths"]!["/health"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]![example]!["value"]!;
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body.ToJsonString()));

        var alignment = (await client.GetHealthAsync(Ct)).LyricsAlignment!;

        var expected = body["lyricsAlignment"]!;
        (alignment.Available, alignment.Model, alignment.ModelCached, alignment.Reason)
            .ShouldBe((expected["available"]!.GetValue<bool>(), expected["model"]!.GetValue<string>(), expected["modelCached"]!.GetValue<bool>(), expected["reason"]?.GetValue<string>()));
        alignment.Auto.ToString().ShouldBe(expected["auto"]!.GetValue<string>(), StringCompareShould.IgnoreCase);
    }

    [Theory]
    [InlineData("always", AlignAuto.Always)]
    [InlineData("never", AlignAuto.Never)]
    public async Task Health_ParsesEveryAutoSetting(string auto, AlignAuto expected)
    {
        var body = ContractJson.WorkerContract.Root["paths"]!["/health"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["cpu"]!["value"]!.DeepClone();
        body["lyricsAlignment"]!["auto"] = auto;
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body.ToJsonString()));

        (await client.GetHealthAsync(Ct)).LyricsAlignment!.Auto.ShouldBe(expected);
    }

    [Fact]
    public async Task Health_OfAnOlderWorkerHasNoLyricsAlignment()
    {
        var body = ContractJson.WorkerContract.Root["paths"]!["/health"]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["examples"]!["cpu"]!["value"]!.DeepClone().AsObject();
        body.Remove("lyricsAlignment");
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body.ToJsonString()));

        (await client.GetHealthAsync(Ct)).LyricsAlignment.ShouldBeNull();
    }

    private static async Task<LyricsDocument> ReadResultAsync(string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));
        return (await client.GetLyricsResultAsync(LookupJobId, Ct)).Lyrics!;
    }

    private static HttpResponseMessage NoStore(HttpResponseMessage response)
    {
        response.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoStore = true };
        return response;
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}

