using System.Net;
using System.Text.Json;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 014 T040: practice-lane calls of the worker client against the worker 1.8.0 examples.</summary>
public sealed class WorkerPracticeClientTests
{
    private const string SectionJobId = "0123456789abcdef0123456789abcdef";
    private const string ScoreViewJobId = "89abcdef0123456789abcdef01234567";
    private const string DocumentJobId = "fedcba9876543210fedcba9876543210";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    internal static WorkerSectionJobRequest SectionRequest() => new()
    {
        Stems = new Dictionary<string, string>
        {
            ["vocals"] = "tracks/3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b/stems/vocals.flac",
            ["drums"] = "tracks/3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b/stems/drums.flac",
        },
        Details = [new WorkerDetailPath("guitar.electric-guitar", "tracks/3f2b8c1e-4d5a-4e6f-8a9b-0c1d2e3f4a5b/detail/guitar/electric-guitar.flac")],
        Grid = new BeatGrid { Bpm = 120, Beats = [0.1, 0.6], Downbeats = [0.1], BeatsPerBar = 4, Confidence = 0.91, Reliable = true },
        DurationSeconds = 64,
    };

    internal static WorkerScoreViewJobRequest ScoreViewRequest() => new()
    {
        Musicxml = "assets/9a8b7c6d-1111-4222-8333-444455556666/score.musicxml",
        Grid = new WorkerBeatGridTempo(120, 4, [0.1, 0.6], [0.1]),
        DurationSeconds = 6.1,
    };

    internal static WorkerSetlistDocumentRequest DocumentRequest() => new()
    {
        Title = "Band night",
        Note = "Bring the capo.",
        GeneratedAt = new DateTimeOffset(2026, 11, 16, 18, 0, 0, TimeSpan.Zero),
        Options = new WorkerSetlistOptions(true, true, true, true),
        Songs = [new WorkerSetlistSong { Position = 1, Title = "Morning Light", DurationSeconds = 222, Sections = [new WorkerSetlistSection("Intro", "intro", 0)], Lyrics = new WorkerSetlistLyrics(["a", "", "b"], false) }],
    };

    [Fact]
    public async Task CreateSection_PostsTheRequest_AndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "section-queued-job"));

        var job = await client.CreateSectionJobAsync(SectionRequest(), Ct);

        (job.JobId, job.Status, job.Result).ShouldBe((SectionJobId, WorkerPracticeJobState.Queued, (WorkerSectionResult?)null));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/section-jobs"));
        using var body = JsonDocument.Parse(sent.BodyText);
        var root = body.RootElement;
        root.GetProperty("stems").GetProperty("vocals").GetString().ShouldEndWith("/stems/vocals.flac");
        root.GetProperty("details")[0].GetProperty("channel").GetString().ShouldBe("guitar.electric-guitar");
        root.GetProperty("grid").GetProperty("reliable").GetBoolean().ShouldBeTrue();
        root.GetProperty("grid").GetProperty("beatsPerBar").GetInt32().ShouldBe(4);
        root.GetProperty("durationSeconds").GetDouble().ShouldBe(64);
    }

    [Fact]
    public async Task GetSection_ParsesTheCompletedResult()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "section-completed-job"));

        var job = (await client.GetSectionJobAsync(SectionJobId, Ct))!;

        job.Status.ShouldBe(WorkerPracticeJobState.Completed);
        var result = job.Result!;
        (result.Analyzer.Name, result.Analyzer.Version, result.Snap).ShouldBe(("stems-novelty", "1.0.0", "downbeat"));
        result.Sections.Count.ShouldBe(5);
        result.Sections[3].ShouldBe(new WorkerDetectedSection("solo", "Electric guitar solo", 40.1, 56.1, "D", "guitar.electric-guitar", 0.66));
        result.Warnings.ShouldBeEmpty();
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/section-jobs/{SectionJobId}");
    }

    [Fact]
    public async Task GetSection_ParsesAFailure_And404IsNull()
    {
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "section-failed-job"));
        var (missing, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        var job = (await failed.GetSectionJobAsync(SectionJobId, Ct))!;

        (job.Status, job.Error, job.Progress).ShouldBe((WorkerPracticeJobState.Failed, "The stems of this track could not be read.", 0.2));
        (await missing.GetSectionJobAsync(SectionJobId, Ct)).ShouldBeNull();
    }

    [Fact]
    public async Task CreateScoreView_SendsTheGridByItsRuntimeType()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "score-view-completed-job"));

        await client.CreateScoreViewJobAsync(ScoreViewRequest(), Ct);

        using var body = JsonDocument.Parse(handler.Requests.Single().BodyText);
        body.RootElement.GetProperty("musicxml").GetString().ShouldEndWith("/score.musicxml");
        body.RootElement.GetProperty("grid").GetProperty("kind").GetString().ShouldBe("beat-grid");
        body.RootElement.GetProperty("grid").GetProperty("downbeats").GetArrayLength().ShouldBe(1);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe("/v1/score-view-jobs");
    }

    [Fact]
    public async Task GetScoreView_ParsesPagesBarsAndEngine()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "score-view-completed-job"));

        var result = (await client.GetScoreViewJobAsync(ScoreViewJobId, Ct))!.Result!;

        result.Pages.ShouldBe([new WorkerScoreViewPage(1, 840, 1188)]);
        result.Bars.Count.ShouldBe(3);
        (result.Bars[1].Number, result.Bars[1].X, result.Bars[1].StartSeconds, result.Bars[1].EndSeconds).ShouldBe((2, 0.2273, 2.1, 4.1));
        result.Engine.ShouldBe(new WorkerAnalyzer("verovio", "6.4.0"));
    }

    [Fact]
    public async Task ScoreViewPage_IsStreamed_FromThePageRoute()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("<svg/>") });
        using var destination = new MemoryStream();

        await client.DownloadScoreViewPageAsync(ScoreViewJobId, 2, destination, Ct);

        System.Text.Encoding.UTF8.GetString(destination.ToArray()).ShouldBe("<svg/>");
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/score-view-jobs/{ScoreViewJobId}/pages/2");
        await Should.ThrowAsync<ArgumentOutOfRangeException>(() => client.DownloadScoreViewPageAsync(ScoreViewJobId, 0, destination, Ct));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ScoreViewPage_AboveTheCap_IsRefused_WithoutBufferingMoreThanTheCap(bool declaresLength)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new SizedContent(WorkerClient.MaxScoreViewPageBytes + 1, declaresLength) });
        using var destination = new MemoryStream();

        await Should.ThrowAsync<WorkerResponseTooLargeException>(() => client.DownloadScoreViewPageAsync(ScoreViewJobId, 1, destination, Ct));

        destination.Length.ShouldBeLessThanOrEqualTo(WorkerClient.MaxScoreViewPageBytes);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SetlistDocument_AboveTheCap_IsRefused_WithoutBufferingMoreThanTheCap(bool declaresLength)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new SizedContent(WorkerClient.MaxSetlistDocumentBytes + 1, declaresLength) });
        using var destination = new MemoryStream();

        await Should.ThrowAsync<WorkerResponseTooLargeException>(() => client.DownloadSetlistDocumentAsync(DocumentJobId, destination, Ct));

        destination.Length.ShouldBeLessThanOrEqualTo(WorkerClient.MaxSetlistDocumentBytes);
    }

    [Fact]
    public async Task Downloads_ExactlyAtTheCap_AreAccepted()
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new SizedContent(WorkerClient.MaxScoreViewPageBytes, declaresLength: false) });
        using var destination = new MemoryStream();

        await client.DownloadScoreViewPageAsync(ScoreViewJobId, 1, destination, Ct);

        destination.Length.ShouldBe(WorkerClient.MaxScoreViewPageBytes);
    }

    [Fact]
    public void TooLargeException_HasTheStandardConstructors()
    {
        new WorkerResponseTooLargeException().Message.ShouldNotBeNullOrWhiteSpace();
        new WorkerResponseTooLargeException("x", new InvalidOperationException()).InnerException.ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task SetlistDocument_CreateGetDownload()
    {
        var (client, handler) = Create(request => request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.EndsWith("/file", StringComparison.Ordinal)
            ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) }
            : StubHttpMessageHandler.Example(HttpStatusCode.OK, "setlist-document-completed-job"));
        using var pdf = new MemoryStream();

        var created = await client.CreateSetlistDocumentJobAsync(DocumentRequest(), Ct);
        var job = (await client.GetSetlistDocumentJobAsync(DocumentJobId, Ct))!;
        await client.DownloadSetlistDocumentAsync(DocumentJobId, pdf, Ct);

        (created.JobId, job.Status, job.Pages).ShouldBe((DocumentJobId, WorkerPracticeJobState.Completed, (int?)3));
        pdf.ToArray().ShouldBe(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        handler.Requests.Select(request => request.Uri.AbsolutePath).ShouldBe(["/v1/setlist-document-jobs", $"/v1/setlist-document-jobs/{DocumentJobId}", $"/v1/setlist-document-jobs/{DocumentJobId}/file"]);
        using var body = JsonDocument.Parse(handler.Requests[0].BodyText);
        var root = body.RootElement;
        root.GetProperty("options").GetProperty("pageBreakPerSong").GetBoolean().ShouldBeTrue();
        root.GetProperty("songs")[0].GetProperty("lyrics").GetProperty("lines").GetArrayLength().ShouldBe(3);
        root.GetProperty("songs")[0].GetProperty("sections")[0].GetProperty("kind").GetString().ShouldBe("intro");
        root.GetProperty("generatedAt").GetString().ShouldBe("2026-11-16T18:00:00+00:00");
    }

    [Fact]
    public async Task Deletes_UseTheJobRoutes_AndTolerate404()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        await client.DeleteSectionJobAsync(SectionJobId, Ct);
        await client.DeleteScoreViewJobAsync(ScoreViewJobId, Ct);
        await client.DeleteSetlistDocumentJobAsync(DocumentJobId, Ct);

        handler.Requests.Select(request => (request.Method, request.Uri.AbsolutePath)).ShouldBe([
            (HttpMethod.Delete, $"/v1/section-jobs/{SectionJobId}"),
            (HttpMethod.Delete, $"/v1/score-view-jobs/{ScoreViewJobId}"),
            (HttpMethod.Delete, $"/v1/setlist-document-jobs/{DocumentJobId}"),
        ]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400-input-path")]
    [InlineData(HttpStatusCode.NotFound, "problem-404-input")]
    [InlineData(HttpStatusCode.Conflict, "problem-409")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-validation")]
    public async Task Problems_KeepTheirStatus(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));

        (await Should.ThrowAsync<WorkerRequestException>(() => client.CreateSectionJobAsync(SectionRequest(), Ct))).StatusCode.ShouldBe(status);
        (await Should.ThrowAsync<WorkerRequestException>(() => client.CreateScoreViewJobAsync(ScoreViewRequest(), Ct))).StatusCode.ShouldBe(status);
        (await Should.ThrowAsync<WorkerRequestException>(() => client.CreateSetlistDocumentJobAsync(DocumentRequest(), Ct))).StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task UnreachableWorker_IsUnavailable_AndBadJobIdsAreRefused()
    {
        var (client, handler) = Create(_ => throw new HttpRequestException("connection refused"));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateSectionJobAsync(SectionRequest(), Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetSectionJobAsync("../etc", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadSetlistDocumentAsync("ABC", Stream.Null, Ct));
        handler.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Health_ParsesThePracticeLane_AndTheStructureAnalyzer()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "health-practice"));

        var health = await client.GetHealthAsync(Ct);

        health.Lanes!.Practice.ShouldBe(new WorkerLaneHealth(2, true));
        health.Lanes.Render.ShouldBe(new WorkerLaneHealth(0, false));
        health.Structure.ShouldBe(new WorkerStructureHealth("stems-novelty", "1.0.0"));
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}

/// <summary>A response body of the given size, with or without a Content-Length (then only counting can catch it).</summary>
internal sealed class SizedContent(long size, bool declaresLength) : HttpContent
{
    protected override async Task SerializeToStreamAsync(Stream stream, System.Net.TransportContext? context)
    {
        var chunk = new byte[64 * 1024];
        for (var left = size; left > 0; left -= chunk.Length)
        {
            await stream.WriteAsync(chunk.AsMemory(0, (int)Math.Min(chunk.Length, left)));
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = size;
        return declaresLength;
    }
}
