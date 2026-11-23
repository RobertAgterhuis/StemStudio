using System.Net;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Tracks;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>
/// Feature 016 T051: inputs per kind (data-model 7.4), grid handling, estimates (7.7), the job applier, the auto starter after
/// the standard separation and the drums split, staleness, and worker 503 / restart handling.
/// </summary>
public sealed class TrackAnalysisServiceTests : ApiTestBase
{
    private static readonly BeatGrid Grid = new() { Bpm = 120, Beats = [0.5], Downbeats = [0.5], Confidence = 0.1, Reliable = false };

    private FakeWorkerAnalysisClient Analysis => Factory.AnalysisWorker;

    private Task<StartResult> StartAsync(Guid trackId, AnalysisKind kind, AnalysisOptions? options = null, bool overwrite = false) =>
        Factory.WithServiceAsync<StartResult, TrackAnalysisService>(service => service.StartAsync(new AnalysisStart(trackId, kind, options ?? TrackAnalysisService.DefaultOptions(kind), overwrite), Ct));

    private Task<AnalysisView?> GetAsync(Guid trackId, AnalysisKind kind) =>
        Factory.WithServiceAsync<AnalysisView?, TrackAnalysisService>(service => service.GetAsync(trackId, kind, Ct));

    [Fact]
    public async Task Chords_SendTheHarmonicStemsTheBassAndTheReliableGrid()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var result = await StartAsync(track.Id, AnalysisKind.Chords, new AnalysisOptions(ChordVocabulary.Full, "chroma-hmm"));

        result.Outcome.ShouldBe(StartOutcome.Stored);
        result.View!.Status.ShouldBe(AnalysisStatus.Queued);
        var sent = Analysis.Requests.Single().ShouldBeOfType<WorkerChordsRequest>();
        sent.Inputs.ShouldBe(new[] { "bass", "guitar", "piano", "other" }.Select(stem => $"tracks/{track.Id:D}/stems/{stem}.flac"));
        (sent.BassInput, sent.Vocabulary, sent.Engine).ShouldBe(($"tracks/{track.Id:D}/stems/bass.flac", "full", "chroma-hmm"));
        sent.Grid.ShouldBe(new WorkerBeatGridTempo(113.5, 4, [0.52, 1.05], [0.52]), new GridComparer());
        var stored = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!;
        (stored.JobId, stored.Options, stored.SourceFingerprint!.Length).ShouldBe((Analysis.Jobs.Keys.Single(), new AnalysisOptions(ChordVocabulary.Full, "chroma-hmm"), 64));
    }

    [Fact]
    public async Task Chords_OfAFourStemTrackWithAnUnreliableGrid_SendBassAndOtherWithoutGrid()
    {
        var track = await Factory.SeedAsync(FourStem());

        await StartAsync(track.Id, AnalysisKind.Chords);

        var sent = Analysis.Requests.Single().ShouldBeOfType<WorkerChordsRequest>();
        sent.Inputs.Select(path => path.Split('/')[^1]).ShouldBe(["bass.flac", "other.flac"]);
        (sent.Grid, sent.Vocabulary, sent.Engine).ShouldBe((null, "simple", null));
    }

    [Fact]
    public async Task Chords_BeforeTheSeparationCompleted_AreRefused()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        (await StartAsync(track.Id, AnalysisKind.Chords)).Outcome.ShouldBe(StartOutcome.TrackNotReady);
        (await StartAsync(track.Id, AnalysisKind.VocalPitch)).Outcome.ShouldBe(StartOutcome.TrackNotReady);
        Analysis.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task UnknownTrack_IsNotFound()
    {
        (await StartAsync(Guid.NewGuid(), AnalysisKind.Chords)).Outcome.ShouldBe(StartOutcome.TrackNotFound);
        (await GetAsync(Guid.NewGuid(), AnalysisKind.Chords)).ShouldBeNull();
        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.DeleteAsync(Guid.NewGuid(), AnalysisKind.Chords, Ct))).ShouldBeFalse();
    }

    [Fact]
    public async Task DrumHits_WithoutASplit_AreUnavailableAndRefused()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var view = (await GetAsync(track.Id, AnalysisKind.DrumHits))!;
        (view.Status, view.Reason, view.Stored).ShouldBe((AnalysisStatus.Unavailable, AnalysisKindStrategies.SplitDrumsFirst, null));
        (await StartAsync(track.Id, AnalysisKind.DrumHits)).Outcome.ShouldBe(StartOutcome.DrumsNotSplit);
    }

    [Fact]
    public async Task DrumHits_SendTheKeptPieces()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        await StartAsync(track.Id, AnalysisKind.DrumHits);

        var inputs = Analysis.Requests.Single().ShouldBeOfType<WorkerDrumHitsRequest>().DrumInputs;
        inputs.ShouldBe(new WorkerDrumInputs($"tracks/{track.Id:D}/detail/drums/kick.flac", $"tracks/{track.Id:D}/detail/drums/snare.flac", null, null, null));
    }

    [Fact]
    public async Task VocalPitch_UsesTheLeadStemWhenKept_ElseTheVocalsStem()
    {
        var split = await Factory.SeedAsync(SyncTestData.DetailCompleted("aaaaaaaaaaa"));
        var backingOnly = await Factory.SeedAsync(DrumsSplitVocalsNot());

        await StartAsync(split.Id, AnalysisKind.VocalPitch);
        await StartAsync(backingOnly.Id, AnalysisKind.VocalPitch);

        Analysis.Requests.Cast<WorkerVocalPitchRequest>().Select(request => (request.Input, request.Source)).ShouldBe(
        [
            ($"tracks/{split.Id:D}/detail/vocals/lead.flac", "vocals.lead"),
            ($"tracks/{backingOnly.Id:D}/stems/vocals.flac", "vocals"),
        ]);
    }

    [Fact]
    public void WithoutVocalOrHarmonicStems_ThePrerequisitesAreMissing()
    {
        var track = WithoutStems();

        AnalysisKindStrategies.Missing(AnalysisKind.VocalPitch, track).ShouldBe(MissingPrerequisite.NoVocals);
        AnalysisKindStrategies.Missing(AnalysisKind.Chords, track).ShouldBe(MissingPrerequisite.NoHarmonicStem);
        AnalysisView.Of(track, AnalysisKind.VocalPitch, null).Reason.ShouldBe(AnalysisKindStrategies.NoVocalStem);
        AnalysisView.Of(track, AnalysisKind.Chords, null).Status.ShouldBe(AnalysisStatus.Unavailable);
        AnalysisKindStrategies.UnavailableReason(MissingPrerequisite.TrackNotReady).ShouldBeNull();
    }

    [Fact]
    public async Task VocalPitchWithoutVocals_IsRefused_AndChordsWithoutHarmonicStems_AreStoredUnavailable()
    {
        var track = await Factory.SeedAsync(WithoutStems());

        (await StartAsync(track.Id, AnalysisKind.VocalPitch)).Outcome.ShouldBe(StartOutcome.NoVocals);
        var chords = await StartAsync(track.Id, AnalysisKind.Chords);

        (chords.Outcome, chords.View!.Status, chords.View.Reason).ShouldBe((StartOutcome.Stored, AnalysisStatus.Unavailable, AnalysisKindStrategies.NoHarmonicStemReason));
        Analysis.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("chords btc cpu ready", 12)]
    [InlineData("chords chroma-hmm cpu ready", 9)]
    [InlineData("chords btc gpu ready", 8)]
    [InlineData("chords btc cpu btc-majmin", 27)]
    [InlineData("vocalPitch - cpu ready", 13)]
    public async Task Estimates_FollowKindEngineDeviceAndWeights(string scenario, int seconds)
    {
        var parts = scenario.Split(' ');
        var (kind, engine, gpu, missingModel) = (Enum.Parse<AnalysisKind>(parts[0], ignoreCase: true), parts[1] == "chroma-hmm" ? parts[1] : null, parts[2] == "gpu", parts[3]);
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = gpu ? ProcessingDeviceKind.Gpu : ProcessingDeviceKind.Cpu }) with
        {
            InstrumentAnalysis = new WorkerInstrumentAnalysisHealth(
                new Dictionary<string, string> { ["btc-majmin"] = missingModel == "btc-majmin" ? "not-downloaded" : "ready", ["btc-large-voca"] = "ready" }, "btc", "pyin"),
        });
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var result = await StartAsync(track.Id, kind, kind == AnalysisKind.Chords ? new AnalysisOptions(ChordVocabulary.Simple, engine) : null);

        result.View!.Stored!.EstimateSeconds.ShouldBe(seconds);
    }

    [Fact]
    public async Task Estimates_UseYinAndTheDrumFactor_AndDefaultsWhenTheHealthFails()
    {
        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Gpu }) with
        {
            InstrumentAnalysis = new WorkerInstrumentAnalysisHealth(new Dictionary<string, string>(), "chroma-hmm", "yin"),
        });
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        (await StartAsync(track.Id, AnalysisKind.VocalPitch)).View!.Stored!.EstimateSeconds.ShouldBe(7);
        (await StartAsync(track.Id, AnalysisKind.DrumHits)).View!.Stored!.EstimateSeconds.ShouldBe(10);
        (await StartAsync(track.Id, AnalysisKind.Chords)).View!.Stored!.EstimateSeconds.ShouldBe(9);

        Worker.OnGetHealth = () => throw new WorkerUnavailableException("down");
        var other = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        (await StartAsync(other.Id, AnalysisKind.Chords)).View!.Stored!.EstimateSeconds.ShouldBe(12);

        Worker.OnGetHealth = () => Task.FromResult(FakeWorkerClient.Health(new WorkerDeviceInfo { Kind = ProcessingDeviceKind.Gpu }));
        var third = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        (await StartAsync(third.Id, AnalysisKind.Chords)).View!.Stored!.EstimateSeconds.ShouldBe(8, "an older worker: defaults on the GPU");
    }

    [Fact]
    public async Task ARunningRun_IsNotStartedTwice_AndConcurrentStartsQueueOneJob()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var release = new TaskCompletionSource();
        Analysis.OnSubmit = () => release.Task;

        var first = StartAsync(track.Id, AnalysisKind.Chords);
        var second = StartAsync(track.Id, AnalysisKind.Chords);
        await Task.Delay(50, Ct);
        release.SetResult();

        (await Task.WhenAll(first, second)).Select(result => result.Outcome).ShouldBe([StartOutcome.Stored, StartOutcome.AnalysisRunning], ignoreOrder: true);
        Analysis.Requests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task EditedChords_AreOnlyReplacedWhenConfirmed()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedReadyAsync(track.Id, Chart(edited: true), new string('1', 64));

        (await StartAsync(track.Id, AnalysisKind.Chords)).Outcome.ShouldBe(StartOutcome.EditsExist);
        (await StartAsync(track.Id, AnalysisKind.Chords, overwrite: true)).View!.Status.ShouldBe(AnalysisStatus.Queued);
    }

    [Fact]
    public async Task AnUnreachableWorker_QueuesNothing_AndARefusalIsStoredAsFailed()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        Analysis.Unavailable = true;

        (await StartAsync(track.Id, AnalysisKind.Chords)).Outcome.ShouldBe(StartOutcome.WorkerUnavailable);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords)).ShouldBeNull();

        Analysis.Unavailable = false;
        Analysis.SubmitFailure = new WorkerRequestException(HttpStatusCode.BadRequest, "input path not allowed");
        var refused = await StartAsync(track.Id, AnalysisKind.Chords);
        (refused.View!.Status, refused.View.Reason).ShouldBe((AnalysisStatus.Failed, $"{TrackAnalysisService.SubmitRejected} input path not allowed"));
    }

    [Fact]
    public async Task TheApplier_StoresProgressTheDocumentAndDeletesTheWorkerJob()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await StartAsync(track.Id, AnalysisKind.Chords);
        var jobId = Analysis.Jobs.Keys.Single();

        Analysis.Run(jobId, 0.42);
        await Factory.SyncAnalysesAsync();
        var running = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!;
        (running.Status, running.Progress, running.Device!.Kind).ShouldBe((AnalysisStatus.Running, 0.42, ProcessingDeviceKind.Cpu));

        Analysis.Complete(jobId);
        await Factory.SyncAnalysesAsync();
        var ready = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!;
        (ready.Status, ready.Engine, ready.JobId).ShouldBe((AnalysisStatus.Ready, "btc", null));
        ready.Document.ShouldBeOfType<ChordChart>().Segments.Count.ShouldBe(3);
        Analysis.Deleted.ShouldBe([jobId]);
    }

    [Fact]
    public async Task TheApplier_StoresFailuresLostJobsAndRefusedResults()
    {
        var tracks = new[] { SyncTestData.CompletedTrack("aaaaaaaaaaa"), SyncTestData.CompletedTrack("bbbbbbbbbbb"), SyncTestData.CompletedTrack("ccccccccccc") };
        foreach (var track in tracks)
        {
            await Factory.SeedAsync(track);
            await StartAsync(track.Id, AnalysisKind.VocalPitch);
        }

        var jobs = Analysis.Requests.Select((_, index) => (index + 1).ToString("x32", System.Globalization.CultureInfo.InvariantCulture)).ToList();
        Analysis.Fail(jobs[0]);
        Analysis.Forget(jobs[1]);
        Analysis.Complete(jobs[2], FakeWorkerAnalysisClient.DrumHits());

        await Factory.SyncAnalysesAsync();

        var reasons = new List<string?>();
        foreach (var track in tracks)
        {
            var analysis = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.VocalPitch))!;
            analysis.Status.ShouldBe(AnalysisStatus.Failed);
            reasons.Add(analysis.Reason);
        }

        reasons.ShouldBe(["The vocal stem is silent, so no melody can be shown.", AnalysisJobApplier.JobLost, AnalysisJobApplier.InvalidResult]);
    }

    [Fact]
    public async Task TheApplier_KeepsTheStateWhileTheWorkerIsUnreachable()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await StartAsync(track.Id, AnalysisKind.Chords);
        Analysis.Unavailable = true;

        await Factory.SyncAnalysesAsync();

        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Queued);
    }

    [Fact]
    public async Task TheApplier_SkipsAnAnalysisDeletedMeanwhile()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.SeedQueuedAsync(track.Id, AnalysisKind.Chords, JobId);
        Analysis.Jobs[JobId] = new WorkerAnalysisJob { AnalysisJobId = JobId, Kind = "chords", Status = WorkerPracticeJobState.Queued };
        Analysis.Complete(JobId);
        Analysis.OnGet = async () =>
        {
            Analysis.OnGet = null;
            await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.DeleteAsync(track.Id, AnalysisKind.Chords, Ct));
        };

        await Factory.SyncAnalysesAsync();

        var analysis = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!;
        (analysis.Status, analysis.Document).ShouldBe((AnalysisStatus.None, null));
    }

    [Fact]
    public async Task Delete_CancelsTheWorkerJobAndClears_EvenWhenTheWorkerIsDown()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await StartAsync(track.Id, AnalysisKind.Chords);
        var jobId = Analysis.Jobs.Keys.Single();

        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.DeleteAsync(track.Id, AnalysisKind.Chords, Ct))).ShouldBeTrue();
        Analysis.Deleted.ShouldBe([jobId]);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.None);

        await StartAsync(track.Id, AnalysisKind.Chords);
        Analysis.Unavailable = true;
        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.DeleteAsync(track.Id, AnalysisKind.Chords, Ct))).ShouldBeTrue();
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.None);
        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.DeleteAsync(track.Id, AnalysisKind.DrumHits, Ct))).ShouldBeTrue("nothing stored is fine");
    }

    [Fact]
    public async Task TheAutoStarter_StartsChordsAfterTheStandardSeparation_ButNotTheMelody()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        await Factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnCompletedAsync(track, Ct);
            return true;
        });

        Analysis.Requests.Select(request => request.Kind).ShouldBe(["chords"]);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.DrumHits)).ShouldBeNull("drum hits wait for the drums split");
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.VocalPitch)).ShouldBeNull();
    }

    [Fact]
    public async Task TheAutoStarter_StartsDrumHitsAfterTheDrumsSplit_ThroughTheDetailSync()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        await Factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnDetailCompletedAsync(track, Ct);
            return true;
        });

        Analysis.Requests.Select(request => request.Kind).ShouldBe(["drum-hits"]);
    }

    [Fact]
    public async Task TheSwitches_TurnTheAutoStartsOff()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:InstrumentViews:AutoChords"] = "false", ["StemStudio:InstrumentViews:AutoDrumHits"] = "false" } };
        var track = await factory.SeedAsync(SyncTestData.DetailCompleted());

        await factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnCompletedAsync(track, Ct);
            await starter.OnDetailCompletedAsync(track, Ct);
            return true;
        });

        factory.AnalysisWorker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangedStems_MakeAReadyChartStale_AndItRunsAgain()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await Factory.WriteStemAsync(track.Id, "bass", 1);
        await StartAsync(track.Id, AnalysisKind.Chords);
        Analysis.Complete(Analysis.Jobs.Keys.Single());
        await Factory.SyncAnalysesAsync();
        var starter = (Func<Task>)(() => Factory.WithServiceAsync<bool, AnalysisAutoStarter>(async auto =>
        {
            await auto.OnCompletedAsync(track, Ct);
            return true;
        }));

        await starter();
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Ready, "same stems: not stale");

        await Factory.WriteStemAsync(track.Id, "bass", 2);
        await starter();

        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Queued);
        Analysis.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AStaleChartWithEdits_StaysStale_AndAStaleMelodyIsNotRerun()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Factory.SeedReadyAsync(track.Id, Chart(edited: true), new string('1', 64));
        await Factory.SeedReadyAsync(track.Id, new VocalPitchDocument { Engine = "pyin", Frames = [0], Source = VocalSource.Vocals }, new string('1', 64));

        await Factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnCompletedAsync(track, Ct);
            return true;
        });

        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Stale);
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.VocalPitch))!.Status.ShouldBe(AnalysisStatus.Stale);
        Analysis.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task AnAutomaticDrumStartWithoutASplit_StoresUnavailable_AndAReturningSplitStartsIt()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        var started = await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.StartAutomaticallyAsync(track, AnalysisKind.DrumHits, Ct));

        started.ShouldBeFalse();
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.DrumHits))!.Status.ShouldBe(AnalysisStatus.Unavailable);

        var split = await Factory.UpdateTrackAsync(track.Id, changed =>
        {
            changed.RequestDetail(T0);
            changed.AssignDetailJob(SyncTestData.DetailJobId, T0);
            changed.CompleteDetail([new Domain.Tracks.Detail.ParentOutcome("drums", Domain.Tracks.Detail.ParentOutcomeKind.Split, [new("kick", 8), new("toms", 8), new("rest", 8)])], T0);
        });
        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.StartAutomaticallyAsync(split, AnalysisKind.DrumHits, Ct))).ShouldBeTrue();
        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.StartAutomaticallyAsync(split, AnalysisKind.DrumHits, Ct))).ShouldBeFalse("never over a run");
    }

    [Fact]
    public async Task AnAutomaticStartBeforeTheSeparation_StoresNothing()
    {
        var track = await Factory.SeedAsync(SyncTestData.UrlTrack());

        (await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.StartAutomaticallyAsync(track, AnalysisKind.Chords, Ct))).ShouldBeFalse();
        (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.Chords)).ShouldBeNull();
    }

    private sealed class GridComparer : IEqualityComparer<WorkerBeatGridTempo?>
    {
        public bool Equals(WorkerBeatGridTempo? x, WorkerBeatGridTempo? y) =>
            x is not null && y is not null && x.Bpm == y.Bpm && x.BeatsPerBar == y.BeatsPerBar && x.Beats.SequenceEqual(y.Beats) && x.Downbeats.SequenceEqual(y.Downbeats);

        public int GetHashCode(WorkerBeatGridTempo? obj) => 0;
    }
}
