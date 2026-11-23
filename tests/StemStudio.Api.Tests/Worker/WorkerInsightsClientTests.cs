using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Handouts;
using StemStudio.Domain.Insights;

namespace StemStudio.Api.Tests.Worker;

/// <summary>Feature 020 T022: insights-lane calls of the worker client against the worker 1.11.0 examples.</summary>
public sealed class WorkerInsightsClientTests
{
    internal const string QualityJobId = "0d1e2f3a4b5c4d6e8f9a0b1c2d3e4f5a";
    internal const string HandoutJobId = "1e2f3a4b5c6d4e7f8a9b0c1d2e3f4a5b";
    private const string Track = "tracks/7d9f2c1e-3b4a-4c5d-8e6f-1a2b3c4d5e6f";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>The request of the worker example <c>QualityRequest</c>, built with the API's records.</summary>
    internal static WorkerQualityJobRequest QualityRequest() => new(
        [
            .. new[] { "vocals", "drums", "bass", "guitar", "piano", "other" }.Select(stem => new WorkerQualityInput(stem, $"{Track}/stems/{stem}.flac", WorkerQualityInputKind.Standard)),
            new WorkerQualityInput("drums.kick", $"{Track}/detail/drums/kick.flac", WorkerQualityInputKind.Detail),
            new WorkerQualityInput("drums.rest", $"{Track}/detail/drums/rest.flac", WorkerQualityInputKind.Rest),
        ],
        [new WorkerDetailParentInput("drums", -25.8, -170.0)],
        new WorkerQualityTitles("Example Song", "Example Band"));

    /// <summary>The request of the worker example <c>BassHandoutRequest</c>, built with the API's records.</summary>
    internal static WorkerHandoutJobRequest HandoutRequest() => new(
        new WorkerHandoutDocument
        {
            Kind = HandoutKind.Song,
            Family = HandoutFamily.Bass,
            FamilyLabel = "Bass",
            Title = "Example Song",
            Subtitle = "Example Band",
            GeneratedOn = "2026-11-16",
            Notice = WorkerHandoutNotice.Fixed,
            Setlist = null,
            Songs =
            [
                new WorkerHandoutSong
                {
                    Title = "Example Song",
                    Artist = "Example Band",
                    ArtworkPath = $"{Track}/artwork/auto/cover.jpg",
                    Facts = new WorkerSongFacts(120, "4/4", "A minor", "chords", 262.0, null),
                    Sections =
                    [
                        new WorkerHandoutSection("Intro", 0, 8, 1, 4, [["Am"], ["F"], ["C"], ["G"]], null),
                        new WorkerHandoutSection("Verse 1", 8, 40, 5, 16, null, 85),
                    ],
                    Lyrics = null,
                    ChordSymbols = ["Am", "F", "C", "G"],
                    Notation =
                    [
                        new WorkerHandoutNotation("Bass - tablature", NotationKind.Tab, "assets/1b2c3d4e-5f60-4718-9a0b-c1d2e3f4a5b6/tab.pdf"),
                        new WorkerHandoutNotation("Bass - score", NotationKind.Score, "assets/2c3d4e5f-6071-4829-8a0b-c1d2e3f4a5b7/score.pdf"),
                    ],
                    PracticeNotes = "Keep the eighths even in the verse; mute the open A.",
                    EntryNotes = null,
                    Include = [IngredientCode.Facts, IngredientCode.FormChart, IngredientCode.ChordChart, IngredientCode.Notation, IngredientCode.PracticeNotes],
                },
            ],
            Attributions = ["Chords detected automatically (StemStudio)"],
            AppVersion = "0.20.0",
        },
        Paper.A4);

    [Fact]
    public async Task CreateQuality_SendsExactlyTheContractExample_AndParsesTheQueuedJob()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "quality-queued-job"));

        var job = await client.CreateQualityJobAsync(QualityRequest(), Ct);

        (job.QualityJobId, job.Status, job.Stage, job.Progress, job.Result).ShouldBe((QualityJobId, WorkerRenderJobState.Queued, (string?)null, 0d, (WorkerQualityResult?)null));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/quality-jobs"));
        SameJson(sent.BodyText, WorkerExamples.Read("quality-request")).ShouldBeTrue(sent.BodyText);
    }

    [Fact]
    public async Task GetQuality_ParsesTheCompletedResult_WithKebabCaseEnums()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "quality-completed-job"));

        var job = (await client.GetQualityJobAsync(QualityJobId, Ct))!;

        (job.Status, job.Progress).ShouldBe((WorkerRenderJobState.Completed, 1d));
        var result = job.Result!;
        (result.AnalyzerVersion, result.DurationSeconds, result.Stems.Count).ShouldBe(("1.0.0", 14.2, 2));
        result.Stems[0].ToDomain().ShouldBe(new StemQuality(
            "vocals", QualityRating.Fair, QualityReason.Bleed, 182.4, 0.69, -6.1,
            new BleedEstimate(QualityLevel.Medium, 0.18, "drums", false, Reliability.Validated),
            result.Stems[0].Artefacts));
        result.Stems[0].Artefacts.ShouldBe(result.Stems[0].Artefacts with { Level = QualityLevel.Low, Index = 0.21, Reliability = Reliability.Validated });
        var piano = result.Stems[1];
        (piano.Rating, piano.RatingReason, piano.Bleed.MainSource, piano.Artefacts.Level, piano.Artefacts.Reliability).ShouldBe((QualityRating.NotPresent, QualityReason.NotPresent, "guitar", QualityLevel.Unknown, Reliability.Indicative));
        piano.Artefacts.Kinds.ShouldBe([ArtefactKind.Flicker]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/quality-jobs/{QualityJobId}");
    }

    [Theory]
    [InlineData("quality-running-job", "analysing", 0.45, null)]
    [InlineData("quality-failed-job", null, 0.2, "The stems of this track could not be read.")]
    public async Task GetQuality_ParsesRunningAndFailedJobs(string example, string? stage, double progress, string? error)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, example));

        var job = (await client.GetQualityJobAsync(QualityJobId, Ct))!;

        (job.Status.ToString().ToLowerInvariant(), job.Stage, job.Progress, job.Error).ShouldBe((example.Split('-')[1], stage, progress, error));
    }

    [Fact]
    public async Task CreateHandout_SendsExactlyTheContractExample()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "handout-queued-job"));

        var job = await client.CreateHandoutJobAsync(HandoutRequest(), Ct);

        (job.HandoutJobId, job.Status).ShouldBe((HandoutJobId, WorkerRenderJobState.Queued));
        var sent = handler.Requests.Single();
        (sent.Method, sent.Uri.AbsolutePath).ShouldBe((HttpMethod.Post, "/v1/handout-jobs"));
        SameJson(sent.BodyText, WorkerExamples.Read("handout-bass-request")).ShouldBeTrue(sent.BodyText);
    }

    [Fact]
    public async Task CreateHandout_WritesTheSetlistBlockAndLyrics_WithEveryNullField()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Accepted, "handout-queued-job"));
        var request = HandoutRequest();
        var song = request.Document.Songs[0] with
        {
            Lyrics = new WorkerHandoutLyrics([new WorkerLyricLine("Hello there", 0, [new WorkerLyricChord("F#m7", 6)]), new WorkerLyricLine(string.Empty, null, [])], "word", "Lyrics: LRCLIB"),
            EntryNotes = "Capo 2",
        };
        var setlist = request with
        {
            Document = request.Document with
            {
                Kind = HandoutKind.Setlist,
                Family = HandoutFamily.Melodic,
                Setlist = new WorkerSetlistBlock("Band night", null, [new WorkerSetlistEntryBlock(1, "Example Song", null, null, null, null, "Capo 2")]),
                Songs = [song],
            },
            Paper = Paper.Letter,
        };

        await client.CreateHandoutJobAsync(setlist, Ct);

        using var body = JsonDocument.Parse(handler.Requests.Single().BodyText);
        var root = body.RootElement;
        (root.GetProperty("paper").GetString(), root.GetProperty("document").GetProperty("kind").GetString(), root.GetProperty("document").GetProperty("family").GetString()).ShouldBe(("letter", "setlist", "melodic"));
        var entry = root.GetProperty("document").GetProperty("setlist").GetProperty("entries")[0];
        (entry.GetProperty("artist").ValueKind, entry.GetProperty("tempoBpm").ValueKind, entry.GetProperty("notes").GetString()).ShouldBe((JsonValueKind.Null, JsonValueKind.Null, "Capo 2"));
        var lyrics = root.GetProperty("document").GetProperty("songs")[0].GetProperty("lyrics");
        (lyrics.GetProperty("chordPlacement").GetString(), lyrics.GetProperty("lines")[0].GetProperty("chords")[0].GetProperty("charIndex").GetInt32()).ShouldBe(("word", 6));
        lyrics.GetProperty("lines")[1].GetProperty("section").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task GetHandout_ParsesTheCompletedResult()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "handout-completed-job"));

        var result = (await client.GetHandoutJobAsync(HandoutJobId, Ct))!.Result!;

        (result.FileName, result.ContentType, result.SizeBytes, result.Pages, result.Warnings.Count).ShouldBe(("handout.pdf", "application/pdf", 184211L, 5, 0));
        result.Sha256.ShouldBe("9f2c1e3b4a4c5d8e6f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e");
        result.Outline.ShouldBe(["Example Song", "Form", "Chord chart", "Notation - Bass - tablature", "Notation - Bass - score", "Practice notes", "About this handout"]);
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/handout-jobs/{HandoutJobId}");
    }

    [Fact]
    public async Task GetHandout_ParsesEveryWarningCode_AndRunningAndFailedJobs()
    {
        var (warned, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "handout-warnings-job"));
        var (running, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "handout-running-job"));
        var (failed, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "handout-failed-job"));

        var warnings = (await warned.GetHandoutJobAsync(HandoutJobId, Ct))!.Result!.Warnings;
        var runningJob = (await running.GetHandoutJobAsync(HandoutJobId, Ct))!;
        var failedJob = (await failed.GetHandoutJobAsync(HandoutJobId, Ct))!;

        warnings.Select(warning => warning.Code).ShouldBe([HandoutWarningCode.NotationScaled, HandoutWarningCode.NotationUnreadable, HandoutWarningCode.MissingGlyphs]);
        warnings[1].Text.ShouldBe("A notation file could not be read: Bass - tablature");
        (runningJob.Status, runningJob.Stage, runningJob.Progress).ShouldBe((WorkerRenderJobState.Running, "notation", 0.7));
        (failedJob.Status, failedJob.Error).ShouldBe((WorkerRenderJobState.Failed, "The handout could not be written"));
    }

    [Fact]
    public async Task HandoutFile_IsStreamed_AndANotCompletedJobIs409()
    {
        var (client, handler) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([0x25, 0x50, 0x44, 0x46]) });
        var (early, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.Conflict, "problem-409-handout"));
        using var pdf = new MemoryStream();

        await client.DownloadHandoutFileAsync(HandoutJobId, pdf, Ct);
        var conflict = await Should.ThrowAsync<WorkerRequestException>(() => early.DownloadHandoutFileAsync(HandoutJobId, Stream.Null, Ct));

        pdf.ToArray().ShouldBe(new byte[] { 0x25, 0x50, 0x44, 0x46 });
        handler.Requests.Single().Uri.AbsolutePath.ShouldBe($"/v1/handout-jobs/{HandoutJobId}/file");
        (conflict.StatusCode, conflict.Detail).ShouldBe((HttpStatusCode.Conflict, "Job is in state running."));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandoutFile_AboveTheCap_IsRefused(bool declaresLength)
    {
        var (client, _) = Create(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new SizedContent(WorkerClient.MaxHandoutBytes + 1, declaresLength) });
        using var destination = new MemoryStream();

        await Should.ThrowAsync<WorkerResponseTooLargeException>(() => client.DownloadHandoutFileAsync(HandoutJobId, destination, Ct));

        destination.Length.ShouldBeLessThanOrEqualTo(WorkerClient.MaxHandoutBytes);
    }

    [Fact]
    public async Task MissingJobs_AreNull_AndDeletesTolerate404()
    {
        var (client, handler) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.NotFound, "problem-404"));

        (await client.GetQualityJobAsync(QualityJobId, Ct)).ShouldBeNull();
        (await client.GetHandoutJobAsync(HandoutJobId, Ct)).ShouldBeNull();
        await client.DeleteQualityJobAsync(QualityJobId, Ct);
        await client.DeleteHandoutJobAsync(HandoutJobId, Ct);

        handler.Requests.Skip(2).Select(request => (request.Method, request.Uri.AbsolutePath)).ShouldBe([
            (HttpMethod.Delete, $"/v1/quality-jobs/{QualityJobId}"),
            (HttpMethod.Delete, $"/v1/handout-jobs/{HandoutJobId}"),
        ]);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, "problem-400-input-path")]
    [InlineData(HttpStatusCode.NotFound, "problem-404-input")]
    [InlineData(HttpStatusCode.Conflict, "problem-409")]
    [InlineData(HttpStatusCode.UnprocessableEntity, "problem-422-notice")]
    public async Task Problems_KeepTheirStatusAndDetail(HttpStatusCode status, string example)
    {
        var (client, _) = Create(_ => StubHttpMessageHandler.Example(status, example));
        using var problem = JsonDocument.Parse(WorkerExamples.Read(example));
        var detail = problem.RootElement.GetProperty("detail").GetString();

        var quality = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateQualityJobAsync(QualityRequest(), Ct));
        var handout = await Should.ThrowAsync<WorkerRequestException>(() => client.CreateHandoutJobAsync(HandoutRequest(), Ct));

        (quality.StatusCode, quality.Detail, handout.StatusCode, handout.Detail).ShouldBe((status, detail, status, detail));
    }

    [Fact]
    public async Task UnreachableWorker_IsUnavailable_AndBadJobIdsAreRefused()
    {
        var (client, handler) = Create(_ => throw new HttpRequestException("connection refused"));

        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateQualityJobAsync(QualityRequest(), Ct));
        await Should.ThrowAsync<WorkerUnavailableException>(() => client.CreateHandoutJobAsync(HandoutRequest(), Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.GetQualityJobAsync("../etc", Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DownloadHandoutFileAsync("ABC", Stream.Null, Ct));
        await Should.ThrowAsync<ArgumentException>(() => client.DeleteHandoutJobAsync("0d1e/../x", Ct));

        handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task InsightsHealth_IsRead_AndAnOlderWorkerHasNone()
    {
        var (current, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "health-insights"));
        var (older, _) = Create(_ => StubHttpMessageHandler.Example(HttpStatusCode.OK, "health-practice"));

        var health = (await current.GetInsightsHealthAsync(Ct))!;

        health.ShouldBe(new WorkerInsightsHealth("fake", "fake-1", new WorkerHandoutRenderer("70.0", "1.56.3"), new WorkerHandoutFonts(true, true)));
        (await older.GetInsightsHealthAsync(Ct)).ShouldBeNull();
    }

    private static bool SameJson(string actual, string expected) => JsonNode.DeepEquals(JsonNode.Parse(actual), JsonNode.Parse(expected));

    private static (WorkerClient Client, StubHttpMessageHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond) =>
        WorkerClientTests.Create(respond);
}
