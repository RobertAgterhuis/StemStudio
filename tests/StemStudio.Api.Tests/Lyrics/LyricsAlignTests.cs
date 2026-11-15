using System.Net;
using System.Net.Http.Json;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Lyrics;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.TestSupport.LyricsTestData;

namespace StemStudio.Api.Tests.Lyrics;

/// <summary>Feature 006 T044: alignment of plain and pasted lyrics (FR-004, FR-014; data-model 3.2, the automatic rule).</summary>
public sealed class LyricsAlignTests : ApiTestBase
{
    private const string Gpu = "NVIDIA GeForce RTX 5070 Ti";

    [Fact]
    public async Task TheAutomaticRule_AlignsAtOnceOnAGpuWorker()
    {
        var track = await Factory.SeedAsync(AlignPending());
        HealthIs(ProcessingDeviceKind.Gpu);
        Worker.OnCreateLyricsAlign = _ => Task.FromResult(AlignJob(WorkerLyricsJobState.Queued) with { EstimatedSeconds = 26.4 });

        await Factory.SyncLyricsAsync();

        var request = Worker.LyricsAligns.Single();
        (request.Mode, request.StandardJobId, request.Text, request.Language, request.Refine).ShouldBe(("align", StandardJobId, "Fake line one\nLa la la", "en", null));
        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.LyricsJobId, lyrics.AutoAlign, lyrics.AlignEstimateSeconds).ShouldBe((LyricsStatus.Aligning, AlignJobId, false, 26));
    }

    [Fact]
    public async Task TheAutomaticRule_OnACpuWorkerShowsTheEstimateInstead()
    {
        var track = await Factory.SeedAsync(AlignPending());
        HealthIs(ProcessingDeviceKind.Cpu);

        await Factory.SyncLyricsAsync();

        Worker.LyricsAligns.ShouldBeEmpty();
        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.AutoAlign, lyrics.AlignEstimateSeconds, lyrics.Reason).ShouldBe((LyricsStatus.AlignPending, false, 185, "Aligning takes about 3 minutes on the CPU."));
        await Factory.SyncLyricsAsync();
        Worker.HealthCalls.ShouldBe(1);
    }

    [Theory]
    [InlineData(AlignAuto.Always, ProcessingDeviceKind.Cpu, true)]
    [InlineData(AlignAuto.Never, ProcessingDeviceKind.Gpu, false)]
    public async Task TheAutomaticRule_FollowsTheWorkerSetting(AlignAuto auto, ProcessingDeviceKind device, bool aligns)
    {
        var track = await Factory.SeedAsync(AlignPending());
        HealthIs(device, auto);

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(aligns ? LyricsStatus.Aligning : LyricsStatus.AlignPending);
    }

    [Fact]
    public async Task TheEstimate_IncludesTheModelDownloadAndAnOlderWorkerCountsAsGpuRuleWithoutModel()
    {
        var notCached = await Factory.SeedAsync(AlignPending("aaaaaaaaaaa"));
        HealthIs(ProcessingDeviceKind.Cpu, modelCached: false);
        await Factory.SyncLyricsAsync();
        var older = await Factory.SeedAsync(AlignPending("bbbbbbbbbbb"));
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Cpu }));

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(notCached.Id))!.Lyrics!.AlignEstimateSeconds.ShouldBe(325);
        (await Factory.FindTrackAsync(older.Id))!.Lyrics!.AlignEstimateSeconds.ShouldBe(325);
    }

    [Fact]
    public async Task TheAutomaticRule_WaitsForTheStandardSeparation()
    {
        var track = SyncTestData.UrlTrack();
        track.ScheduleLookup(new LookupQuery("Fake Song", null, null, 262.0), SyncTestData.T0);
        track.ScheduleLyricsLookup(SyncTestData.T0);
        track.Lyrics!.ApplyLookup(LyricsOutcome.Found, Document("lyrics-result-plain"), SyncTestData.T0);
        await Factory.SeedAsync(track);
        HealthIs(ProcessingDeviceKind.Gpu);

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.AutoAlign).ShouldBe((LyricsStatus.AlignPending, true));
        Worker.HealthCalls.ShouldBe(0);
    }

    [Fact]
    public async Task AnUnreachableWorker_KeepsTheAutomaticRulePending()
    {
        var track = await Factory.SeedAsync(AlignPending());
        Worker.OnGetHealth = () => throw new WorkerUnavailableException();

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.AutoAlign.ShouldBeTrue();
    }

    [Fact]
    public async Task ARefusedHealthCheck_KeepsTheAutomaticRulePending()
    {
        var track = await Factory.SeedAsync(AlignPending());
        Worker.OnGetHealth = () => throw new WorkerRequestException(HttpStatusCode.NotFound, "Not found");

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.AutoAlign).ShouldBe((LyricsStatus.AlignPending, true));
    }

    [Fact]
    public async Task ARejectedAlignment_FailsWithTheWorkerText()
    {
        var track = await Factory.SeedAsync(AlignPending());
        HealthIs(ProcessingDeviceKind.Gpu);
        Worker.OnCreateLyricsAlign = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, "The vocal stem of this track is no longer available in the worker; re-import the track.");

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason).ShouldBe((LyricsStatus.Failed, "The vocal stem of this track is no longer available in the worker; re-import the track."));
    }

    [Fact]
    public async Task ARunningAlignment_ReportsProgressDeviceAndEstimate()
    {
        var track = await Factory.SeedAsync(Aligning());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(AlignJob(WorkerLyricsJobState.Running, 0.4) with { Device = new WorkerJobDevice(ProcessingDeviceKind.Gpu, Gpu, false, null), EstimatedSeconds = 30 });

        await Factory.SyncLyricsAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getLyrics");
        (await response.BodyAsync<LyricsDto>()).Alignment.ShouldBe(new LyricsAlignmentDto(30, new JobDeviceDto(ProcessingDeviceKindDto.Gpu, Gpu, false, null), 40));
    }

    [Fact]
    public async Task AnInvalidAlignmentDevice_IsIgnored()
    {
        var track = await Factory.SeedAsync(Aligning());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(AlignJob(WorkerLyricsJobState.Running, 0.5) with { Device = new WorkerJobDevice(ProcessingDeviceKind.Cpu, "named cpu", false, null) });

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.AlignProgress, lyrics.AlignDevice, lyrics.AlignEstimateSeconds).ShouldBe((50, null, null));
    }

    [Fact]
    public async Task ACompletedAlignment_IsFoundWithAlignedTiming()
    {
        var track = await Factory.SeedAsync(Aligning());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(AlignJob(WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => Task.FromResult(Result("lyrics-result-aligned"));

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Document!.Origin, lyrics.Document.SyncLevel).ShouldBe((LyricsStatus.Found, LyricsOrigin.Aligned, SyncLevel.Word));
        Worker.LyricsDeletes.ShouldBe([AlignJobId]);
        (await (await Client.GetAsync(new Uri(ApiRoutes.Lyrics(track.Id), UriKind.Relative), Ct)).BodyAsync<LyricsDto>()).Credit.ShouldBe("Lyrics: entered by you; timing: automatic alignment");
    }

    [Theory]
    [InlineData(WorkerLyricsJobState.Failed, "The lyrics could not be aligned to the vocals.")]
    [InlineData(WorkerLyricsJobState.Cancelled, Api.Lyrics.LyricsJobApplier.CancelledReason)]
    public async Task AFailedAlignment_KeepsTheTextForAnotherTry(WorkerLyricsJobState state, string reason)
    {
        var track = await Factory.SeedAsync(Aligning());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(AlignJob(state));

        await Factory.SyncLyricsAsync();

        var lyrics = (await Factory.FindTrackAsync(track.Id))!.Lyrics!;
        (lyrics.Status, lyrics.Reason, lyrics.HasText).ShouldBe((LyricsStatus.Failed, reason, true));
        using var retry = await PostAsync(ApiRoutes.LyricsAlign(track.Id));
        retry.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    }

    [Fact]
    public async Task ACompletedAlignmentWithoutLyrics_Fails()
    {
        var track = await Factory.SeedAsync(Aligning());
        Worker.OnGetLyricsJob = _ => Task.FromResult<WorkerLyricsJob?>(AlignJob(WorkerLyricsJobState.Completed, 1));
        Worker.OnGetLyricsResult = _ => Task.FromResult(new WorkerLyricsResult { Outcome = WorkerLyricsOutcome.Found });

        await Factory.SyncLyricsAsync();

        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.Reason.ShouldBe(TrackLyrics.UnalignableReason);
    }

    [Fact]
    public async Task AnAlignmentWithoutAJob_IsSubmittedByTheSynchroniser()
    {
        var seeded = AlignPending();
        seeded.Lyrics!.StartAlign(SyncTestData.T0);
        var track = await Factory.SeedAsync(seeded);

        await Factory.SyncLyricsAsync();

        Worker.LyricsAligns.ShouldHaveSingleItem();
        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.LyricsJobId.ShouldNotBeNull();
    }

    [Fact]
    public async Task Paste_OnACpuWorkerWaitsWithTheEstimate()
    {
        var track = await Factory.SeedAsync(WithResult("lyrics-result-not-found"));
        HealthIs(ProcessingDeviceKind.Cpu);

        using var response = await PasteAsync(track.Id, new SetLyricsRequest("Fake line one\nLa la la", "en"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        await ContractAssert.MatchesAsync(response, "setLyrics");
        var dto = await response.BodyAsync<LyricsDto>();
        (dto.Status, dto.Source, dto.SyncLevel, dto.Credit, dto.Language, dto.Reason).ShouldBe((LyricsStatusDto.AlignPending, LyricsSourceDto.User, SyncLevelDto.Plain, "Lyrics: entered by you", "en", "Aligning takes about 3 minutes on the CPU."));
        dto.Alignment.ShouldBe(new LyricsAlignmentDto(185, null, null));
        dto.Lines.Select(line => line.Text).ShouldBe(FixtureWords);
    }

    [Fact]
    public async Task Paste_OnAGpuWorkerAlignsAtOnce()
    {
        var track = await Factory.SeedAsync(Found());
        HealthIs(ProcessingDeviceKind.Gpu);

        using var response = await PasteAsync(track.Id, new SetLyricsRequest("Fake line one\nLa la la"));

        await ContractAssert.MatchesAsync(response, "setLyrics");
        (await response.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Aligning);
        Worker.LyricsAligns.Single().Language.ShouldBeNull();
    }

    [Fact]
    public async Task Paste_WithAnUnreachableWorkerAlignsLater()
    {
        var track = await Factory.SeedAsync(Found());
        Worker.OnGetHealth = () => throw new WorkerUnavailableException();

        using var response = await PasteAsync(track.Id, new SetLyricsRequest("Fake line one"));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.AutoAlign.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", null, "text")]
    [InlineData("bell\u0007", null, "text")]
    [InlineData("Fake line one", "English", "language")]
    public async Task Paste_InvalidTextIs422(string text, string? language, string field)
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await PasteAsync(track.Id, new SetLyricsRequest(text, language));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        await ContractAssert.MatchesAsync(response, "setLyrics");
        (await response.FieldErrorsAsync(field)).ShouldNotBeEmpty();
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.Found);
    }

    [Fact]
    public async Task Paste_TooLongTextIs422()
    {
        var track = await Factory.SeedAsync(Found());

        using var response = await PasteAsync(track.Id, new SetLyricsRequest(new string('x', ContractLimits.MaxLyricsChars + 1)));

        response.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Paste_WhileAligningIs409AndUnknownTracksAre404()
    {
        var track = await Factory.SeedAsync(Aligning());

        using var busy = await PasteAsync(track.Id, new SetLyricsRequest("Fake line one"));
        using var unknown = await PasteAsync(Guid.NewGuid(), new SetLyricsRequest("Fake line one"));

        busy.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await busy.JsonAsync()).GetProperty("type").GetString().ShouldBe("lyrics-busy");
        await ContractAssert.MatchesAsync(busy, "setLyrics");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(unknown, "setLyrics");
    }

    [Fact]
    public async Task Align_SubmitsAtOnceWithTheEstimate()
    {
        var track = await Factory.SeedAsync(AlignPending());
        Worker.OnCreateLyricsAlign = _ => Task.FromResult(AlignJob(WorkerLyricsJobState.Queued) with { EstimatedSeconds = 420 });

        using var response = await PostAsync(ApiRoutes.LyricsAlign(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(response, "alignLyrics");
        var dto = await response.BodyAsync<LyricsDto>();
        (dto.Status, dto.Alignment).ShouldBe((LyricsStatusDto.Aligning, new LyricsAlignmentDto(420, null, 0)));
        (await Factory.FindTrackAsync(track.Id))!.Lyrics!.LyricsJobId.ShouldBe(AlignJobId);
    }

    [Fact]
    public async Task Align_RefinesLineSyncedLyricsWithTheirDocument()
    {
        var track = await Factory.SeedAsync(WithResult("lyrics-result-line"));

        using var response = await PostAsync(ApiRoutes.LyricsAlign(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        Worker.LyricsAligns.Single().Refine!.SyncLevel.ShouldBe(SyncLevel.Line);
    }

    [Theory]
    [InlineData("lyrics-result-not-found", "lyrics-no-text")]
    [InlineData("lyrics-result-word", "lyrics-timed")]
    public async Task Align_WithoutAlignableTextIs409(string example, string type)
    {
        var track = await Factory.SeedAsync(WithResult(example));

        using var response = await PostAsync(ApiRoutes.LyricsAlign(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "alignLyrics");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe(type);
    }

    [Fact]
    public async Task Align_WhileAligningOrBeforeTheSeparationIs409()
    {
        var aligning = await Factory.SeedAsync(Aligning("aaaaaaaaaaa"));
        var processing = SyncTestData.UrlTrack("bbbbbbbbbbb");
        processing.EnsureLyrics(SyncTestData.T0).Paste(LyricsDocument.FromUserText("Fake line one", null), SyncTestData.T0);
        await Factory.SeedAsync(processing);

        using var busy = await PostAsync(ApiRoutes.LyricsAlign(aligning.Id));
        using var notReady = await PostAsync(ApiRoutes.LyricsAlign(processing.Id));

        (await busy.JsonAsync()).GetProperty("type").GetString().ShouldBe("lyrics-busy");
        (await notReady.JsonAsync()).GetProperty("type").GetString().ShouldBe("track-not-ready");
        await ContractAssert.MatchesAsync(notReady, "alignLyrics");
    }

    [Fact]
    public async Task Align_WithAnUnreachableWorkerIs503AndChangesNothing()
    {
        var track = await Factory.SeedAsync(AlignPending());
        Worker.OnCreateLyricsAlign = _ => throw new WorkerUnavailableException();

        using var response = await PostAsync(ApiRoutes.LyricsAlign(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(response, "alignLyrics");
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.AlignPending);
    }

    [Fact]
    public async Task Align_RejectedByTheWorkerIs409WithItsText()
    {
        var track = await Factory.SeedAsync(AlignPending());
        Worker.OnCreateLyricsAlign = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, "Job 3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c is in state separating.");

        using var response = await PostAsync(ApiRoutes.LyricsAlign(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "alignLyrics");
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("Job 3f2b8c1d9e4a4f6b8a7c5d3e2f1a0b9c is in state separating.");
        (await Factory.FindTrackAsync(track.Id))!.LyricsStatus.ShouldBe(LyricsStatus.AlignPending);
    }

    [Fact]
    public async Task CancelAlignment_GoesBackToAlignPendingAndRemovesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(Aligning());

        using var response = await PostAsync(ApiRoutes.LyricsAlignCancel(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "cancelLyricsAlignment");
        (await response.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.AlignPending);
        Worker.LyricsDeletes.ShouldBe([AlignJobId]);
    }

    [Fact]
    public async Task CancelAlignment_OfARefinementGoesBackToTheLineSyncedLyrics()
    {
        var seeded = WithResult("lyrics-result-line");
        seeded.Lyrics!.StartAlign(SyncTestData.T0);
        var track = await Factory.SeedAsync(seeded);
        Worker.OnDeleteLyrics = _ => throw new WorkerUnavailableException();

        using var response = await PostAsync(ApiRoutes.LyricsAlignCancel(track.Id));

        (await response.BodyAsync<LyricsDto>()).Status.ShouldBe(LyricsStatusDto.Found);
        Worker.LyricsDeletes.ShouldBeEmpty();
    }

    [Fact]
    public async Task CancelAlignment_WithoutAnAlignmentIs409AndUnknownTracksAre404()
    {
        var track = await Factory.SeedAsync(Found());

        using var conflict = await PostAsync(ApiRoutes.LyricsAlignCancel(track.Id));
        using var unknown = await PostAsync(ApiRoutes.LyricsAlignCancel(Guid.NewGuid()));

        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(conflict, "cancelLyricsAlignment");
        (await conflict.JsonAsync()).GetProperty("type").GetString().ShouldBe("lyrics-not-aligning");
        await ContractAssert.MatchesAsync(unknown, "cancelLyricsAlignment");
        (await PostAsync(ApiRoutes.LyricsAlign(Guid.NewGuid()))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static WorkerLyricsJob AlignJob(WorkerLyricsJobState state, double progress = 0) => FakeWorkerClient.LyricsJob(AlignJobId, WorkerLyricsJobMode.Align, state, progress);

    private void HealthIs(ProcessingDeviceKind kind, AlignAuto auto = AlignAuto.Gpu, bool modelCached = true)
    {
        Worker.OnGetHealth = () => Task.FromResult(Health(kind, auto, modelCached));
        Worker.OnCreateLyricsAlign ??= _ => Task.FromResult(AlignJob(WorkerLyricsJobState.Queued));
    }

    private Task<HttpResponseMessage> PasteAsync(Guid trackId, SetLyricsRequest request) =>
        Client.PutAsJsonAsync(new Uri(ApiRoutes.Lyrics(trackId), UriKind.Relative), request, Ct);

    private Task<HttpResponseMessage> PostAsync(string path) => Client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);
}
