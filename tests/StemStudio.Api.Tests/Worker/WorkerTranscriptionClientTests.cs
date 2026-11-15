using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Scores;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 008 T036: transcription job calls of the worker client against the worker 1.6.0 examples.</summary>
public sealed class WorkerTranscriptionClientTests
{
    private const string JobId = "c4e6a8b0d2f4416385a7c9e1b3d5f7a9";
    private const string Track = "0b7e4c1a-9d2f-4e8b-a1c3-5f6e7d8c9b0a";
    private static readonly double[] Beats = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_PostsTheBassExampleAndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "transcription-queued-job"));
        var request = new WorkerTranscriptionJobRequest
        {
            Profile = ScoreProfile.Bass,
            Quality = ScoreQuality.Accurate,
            Input = new WorkerTranscriptionInput($"tracks/{Track}/stems/bass.flac"),
            Grid = new WorkerBeatGridTempo(120.0, 4, Beats, [0.5, 2.5]),
            Titles = new WorkerScoreTitles("Fake Song - bass (sheet music)", "Fake Song - The Fixture Band", "Bass"),
        };

        var job = await client.CreateTranscriptionJobAsync(request, Ct);

        (job.TranscriptionJobId, job.Status, job.Progress, job.Result).ShouldBe((JobId, WorkerRenderJobState.Queued, 0, null));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/transcription-jobs"));
        JsonNode.DeepEquals(JsonNode.Parse(sent.BodyText), JsonNode.Parse(WorkerExamples.Read("transcription-request-bass"))).ShouldBeTrue(sent.BodyText);
    }

    [Fact]
    public async Task Create_SendsTheDrumInputsAndTheResolution()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "transcription-queued-job"));
        var piece = (string name) => $"tracks/{Track}/detail/drums/{name}.flac";
        var request = new WorkerTranscriptionJobRequest
        {
            Profile = ScoreProfile.Drums,
            Quality = ScoreQuality.Accurate,
            DrumInputs = new WorkerDrumInputs(piece("kick"), piece("snare"), piece("toms"), piece("hihat"), piece("cymbals")),
            Grid = new WorkerBeatGridTempo(120.0, 4, Beats, [0.5, 2.5]),
            Resolution = ScoreResolution.Sixteenth,
            Titles = new WorkerScoreTitles("Fake Song - drums (sheet music)", "Fake Song - The Fixture Band", "Drums"),
        };

        await client.CreateTranscriptionJobAsync(request, Ct);

        var sent = handler.Requests.Single().BodyText;
        JsonNode.DeepEquals(JsonNode.Parse(sent), JsonNode.Parse(WorkerExamples.Read("transcription-request-drums"))).ShouldBeTrue(sent);
    }

    [Fact]
    public async Task Create_SendsAManualGridARangeAndAKey()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "transcription-queued-job"));
        var request = new WorkerTranscriptionJobRequest
        {
            Profile = ScoreProfile.Piano,
            Quality = ScoreQuality.Accurate,
            Input = new WorkerTranscriptionInput("assets/3c9a1e7b-5d2f-4a6c-8e0b-1f2a3b4c5d6e/audio.flac"),
            Range = new WorkerRenderRange(30.0, 75.0),
            Grid = new WorkerManualTempo(96.0, 3, 0.4),
            Resolution = ScoreResolution.Eighth,
            KeyOverride = "E- major",
            Titles = new WorkerScoreTitles("Piano part (sheet music)", "Fake Song - The Fixture Band", "Piano"),
        };

        await client.CreateTranscriptionJobAsync(request, Ct);

        var sent = handler.Requests.Single().BodyText;
        JsonNode.DeepEquals(JsonNode.Parse(sent), JsonNode.Parse(WorkerExamples.Read("transcription-request-piano"))).ShouldBeTrue(sent);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400-input-path")]
    [InlineData(HttpStatusCode.NotFound, "problem-404-input")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-validation")]
    public async Task Create_RejectionsBecomeRequestExceptions(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));
        var request = new WorkerTranscriptionJobRequest { Profile = ScoreProfile.Bass, Grid = new WorkerManualTempo(120, 4, 0), Titles = new WorkerScoreTitles("x", null, null) };

        (await Should.ThrowAsync<WorkerRequestException>(() => client.CreateTranscriptionJobAsync(request, Ct))).StatusCode.ShouldBe(status);
    }

    [Fact]
    public async Task Get_ParsesTheRunningJobWithItsDevice()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "transcription-running-job"));

        var job = (await client.GetTranscriptionJobAsync(JobId, Ct))!;

        (job.Status, job.Stage, job.Progress, job.Device).ShouldBe((WorkerRenderJobState.Running, "transcribing", 0.42, new WorkerJobDevice(ProcessingDeviceKind.Cpu, null, false, null)));
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/transcription-jobs/{JobId}");
    }

    [Fact]
    public async Task Get_ParsesTheCompletedResult()
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "transcription-completed-job"));

        var result = (await client.GetTranscriptionJobAsync(JobId, Ct))!.Result!;

        result.Files.Select(file => (file.Role, file.FileName, file.ContentType)).ShouldBe(
            [("pdf", "score.pdf", "application/pdf"), ("musicxml", "score.musicxml", "application/vnd.recordare.musicxml+xml"), ("midi", "score.mid", "audio/midi")]);
        (result.Files[0].SizeBytes, result.Files[0].Sha256).ShouldBe((183296, "4a5b6c7d8e9f00112233445566778899aabbccddeeff00112233445566778899"));
        var summary = result.Summary;
        (summary.Profile, summary.Engine, summary.Quality, summary.Bpm, summary.TimeSignature, summary.Bars, summary.Notes, summary.Pages, summary.Resolution)
            .ShouldBe((ScoreProfile.Bass, ScoreEngine.BasicPitch, null, 118, "4/4", 125, 386, 3, ScoreResolution.Sixteenth));
        summary.Key.ShouldBe(new KeyInfo("A-", KeyMode.Major, 0.83, KeySource.Estimated));
        summary.Warnings.Single().ShouldBe(new ScoreWarning(ScoreWarningCode.NotesCleaned, "87 very short or out-of-range notes were removed."));
        summary.Attribution.ShouldBeNull();
    }

    [Theory]
    [InlineData("piano-crnn", ScoreEngine.PianoCrnn)]
    [InlineData("drum-onsets", ScoreEngine.DrumOnsets)]
    [InlineData("fake", ScoreEngine.Fake)]
    public async Task Get_ParsesEveryEngineAndWarningCode(string engine, ScoreEngine expected)
    {
        var body = JsonNode.Parse(WorkerExamples.Read("transcription-completed-job"))!;
        body["result"]!["summary"]!["engine"] = engine;
        body["result"]!["summary"]!["warnings"] = JsonNode.Parse("""[{"code":"missing-drum-piece","text":"No toms stem was available; that part is empty."},{"code":"cpu-piano","text":"The piano model ran on the CPU."}]""");
        var (client, _) = Create(_ => StubHttpMessageHandler.Json(HttpStatusCode.OK, body.ToJsonString()));

        var summary = (await client.GetTranscriptionJobAsync(JobId, Ct))!.Result!.Summary;

        summary.Engine.ShouldBe(expected);
        summary.Warnings.Select(warning => warning.Code).ShouldBe([ScoreWarningCode.MissingDrumPiece, ScoreWarningCode.CpuPiano]);
    }

    [Fact]
    public async Task Get_ParsesTheFailureAnd404IsNull()
    {
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "transcription-failed-job"));
        var (missing, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        var job = (await failed.GetTranscriptionJobAsync(JobId, Ct))!;

        (job.Status, job.Error).ShouldBe((WorkerRenderJobState.Failed, "No notes were detected in this audio."));
        (await missing.GetTranscriptionJobAsync(JobId, Ct)).ShouldBeNull();
    }

    [Theory]
    [InlineData("pdf", "application/pdf")]
    [InlineData("musicxml", "application/vnd.recordare.musicxml+xml")]
    [InlineData("midi", "audio/midi")]
    public async Task DownloadFile_StreamsEveryRole(string role, string contentType)
    {
        var (client, handler) = Create(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };
            response.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return response;
        });
        using var destination = new MemoryStream();

        await client.DownloadTranscriptionFileAsync(JobId, role, destination, Ct);

        destination.ToArray().ShouldBe([1, 2, 3]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/transcription-jobs/{JobId}/files/{role}");
    }

    [Fact]
    public async Task DownloadFile_409AndUnknownRoles()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-transcription"));
        using var destination = new MemoryStream();

        (await Should.ThrowAsync<WorkerRequestException>(() => client.DownloadTranscriptionFileAsync(JobId, "pdf", destination, Ct))).Detail.ShouldBe("Transcription job is in state running.");
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadTranscriptionFileAsync(JobId, "audio", destination, Ct));
        handler.Requests.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Delete_IsIdempotent(HttpStatusCode status)
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(status));

        await client.DeleteTranscriptionJobAsync(JobId, Ct);

        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Delete, $"/v1/transcription-jobs/{JobId}"));
    }

    [Fact]
    public async Task InvalidJobIds_AreRejectedWithoutARequest()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK));
        using var destination = new MemoryStream();

        await Should.ThrowAsync<ArgumentException>(() => client.GetTranscriptionJobAsync("../x", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadTranscriptionFileAsync("x", "pdf", destination, Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DeleteTranscriptionJobAsync("x", Ct));
        handler.Requests.ShouldBeEmpty();
    }

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
