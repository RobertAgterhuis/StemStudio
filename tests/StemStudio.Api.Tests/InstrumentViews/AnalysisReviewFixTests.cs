using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using StemStudio.Domain.Rendering;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>
/// Round 016 review fixes: the estimate of a never-run analysis (M3), a refused job read does not starve the sync pass (M4),
/// vanished inputs make a ready analysis stale (L3) and the analysis lock is per track and kind (L4).
/// </summary>
public sealed class AnalysisReviewFixTests : ApiTestBase
{
    private const string OlderJob = "0000000000000000000000000000000a";
    private const string NewerJob = "0000000000000000000000000000000b";

    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private static Uri Url(string route) => new(route, UriKind.Relative);

    private static Task<StartResult> StartAsync(ApiFactory factory, Guid trackId, AnalysisKind kind) =>
        factory.WithServiceAsync<StartResult, TrackAnalysisService>(service =>
            service.StartAsync(new AnalysisStart(trackId, kind, TrackAnalysisService.DefaultOptions(kind), false), Ct));

    private static ApiFactory SlowFactory(SlowFingerprinter slow) => new()
    {
        ConfigureServices = services => services.AddSingleton<IStemFingerprinter>(provider => slow.Wrap(ActivatorUtilities.CreateInstance<StemFingerprinter>(provider))),
    };

    private static Task<bool> DetailCompletedAsync(ApiFactory factory, Track track) =>
        factory.WithServiceAsync<bool, AnalysisAutoStarter>(async starter =>
        {
            await starter.OnDetailCompletedAsync(track, Ct);
            return true;
        });

    [Fact]
    public async Task M3_ANeverRunMelodyOfALongTrack_ShowsItsEstimate_SoTheUiCanAskFirst()
    {
        var track = await Factory.SeedAsync(FourStem(durationSeconds: 900));

        using var response = await Client.GetAsync(Url(ApiRoutes.VocalPitch(track.Id)), Ct);

        var dto = await response.BodyAsync<TrackAnalysisDto<VocalPitchDocumentDto>>();
        (dto.Status, dto.EstimateSeconds).ShouldBe((AnalysisStatusDto.None, 37), "900 s (the longest track) x 0.035 (pyin on the CPU) + 5 s, rounded up");
        Factory.AnalysisWorker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task M3_AnUnavailableAnalysisWithoutInputs_HasNoEstimate_ButOneWhoseInputsReturned_Has()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var unsplit = await Client.GetAsync(Url(ApiRoutes.DrumHits(track.Id)), Ct);
        var none = await unsplit.BodyAsync<TrackAnalysisDto<DrumHitsDocumentDto>>();
        (none.Status, none.EstimateSeconds).ShouldBe((AnalysisStatusDto.Unavailable, null));

        await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.StartAutomaticallyAsync(track, AnalysisKind.DrumHits, Ct));
        await Factory.UpdateTrackAsync(track.Id, SplitKickAndSnare);

        using var split = await Client.GetAsync(Url(ApiRoutes.DrumHits(track.Id)), Ct);
        var stored = await split.BodyAsync<TrackAnalysisDto<DrumHitsDocumentDto>>();
        (stored.Status, stored.EstimateSeconds).ShouldBe((AnalysisStatusDto.Unavailable, 10), "213 s x 0.02 + 5 s, rounded up");
    }

    [Fact]
    public async Task M4_ARefusedJobRead_FailsThatAnalysis_AndTheOthersStillComplete()
    {
        var (first, second) = await TwoCompletedChordJobsAsync();
        Factory.AnalysisWorker.JobFailure = jobId => jobId == OlderJob ? new WorkerRequestException(HttpStatusCode.BadRequest, "unknown job id") : null;

        await Factory.SyncAnalysesAsync();

        var refused = (await Factory.FindAnalysisAsync(first, AnalysisKind.Chords))!;
        (refused.Status, refused.Reason).ShouldBe((AnalysisStatus.Failed, AnalysisJobApplier.JobLost), "data-model 7.2: queued/running -> failed when the worker no longer serves the job");
        (await Factory.FindAnalysisAsync(second, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Ready);
    }

    [Fact]
    public async Task M4_ARefusedDeleteAfterTheFinish_IsBestEffort_AndTheNextJobIsStillSynced()
    {
        var (first, second) = await TwoCompletedChordJobsAsync();
        Factory.AnalysisWorker.DeleteFailure = jobId => jobId == OlderJob ? new WorkerRequestException(HttpStatusCode.NotFound, "no such job") : null;

        await Factory.SyncAnalysesAsync();

        (await Factory.FindAnalysisAsync(first, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Ready);
        (await Factory.FindAnalysisAsync(second, AnalysisKind.Chords))!.Status.ShouldBe(AnalysisStatus.Ready);
        Factory.AnalysisWorker.Deleted.ShouldContain(NewerJob);
    }

    [Fact]
    public async Task L3_ARedoneDrumsSplitWithoutDrumStems_MakesReadyDrumHitsStaleWithAReason()
    {
        using var factory = new ApiFactory { Settings = new Dictionary<string, string?> { ["StemStudio:InstrumentViews:AutoDrumHits"] = "false" } };
        var track = await ResplitWithoutDrumsAsync(factory);

        await DetailCompletedAsync(factory, track);

        var stale = (await factory.FindAnalysisAsync(track.Id, AnalysisKind.DrumHits))!;
        stale.Status.ShouldBe(AnalysisStatus.Stale);
        stale.Reason.ShouldBe($"{TrackAnalysisService.InputsGone} {AnalysisKindStrategies.SplitDrumsFirst}");
        stale.Document.ShouldNotBeNull("stale keeps the old document");
    }

    [Fact]
    public async Task L3_WithTheAutoRuleOn_TheStaleDrumHitsBecomeUnavailable()
    {
        var track = await ResplitWithoutDrumsAsync(Factory);

        await DetailCompletedAsync(Factory, track);

        var analysis = (await Factory.FindAnalysisAsync(track.Id, AnalysisKind.DrumHits))!;
        (analysis.Status, analysis.Reason, analysis.Document).ShouldBe((AnalysisStatus.Unavailable, AnalysisKindStrategies.SplitDrumsFirst, null));
        Factory.AnalysisWorker.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task L4_AChordEditOnTrackB_IsNotBlockedByASlowStartOnTrackA()
    {
        var slow = new SlowFingerprinter();
        using var factory = SlowFactory(slow);
        using var client = factory.CreateClient();
        var (trackA, trackB) = (await factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa")), await factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb")));
        await factory.SeedReadyAsync(trackB.Id, Chart(), new string('1', 64));
        var startA = slow.HoldWhile(trackA.Id, () => StartAsync(factory, trackA.Id, AnalysisKind.Chords));
        await slow.Entered.Task.WaitAsync(Patience, Ct);

        using var edit = await client.PutAsJsonAsync(Url($"/api/tracks/{trackB.Id}/chords/segments/1"), new EditChordRequest { Label = "A:min" }, Ct).WaitAsync(Patience, Ct);
        var startB = await StartAsync(factory, trackB.Id, AnalysisKind.VocalPitch).WaitAsync(Patience, Ct);

        (edit.StatusCode, startB.Outcome, startA.IsCompleted).ShouldBe((HttpStatusCode.OK, StartOutcome.Stored, false), "track A is still fingerprinting");
        slow.Release();
        (await startA).View!.Status.ShouldBe(AnalysisStatus.Queued);
    }

    [Fact]
    public async Task L4_TwoStartsOfOneTrackAndKind_StillQueueOneJob()
    {
        var slow = new SlowFingerprinter();
        using var factory = SlowFactory(slow);
        var track = await factory.SeedAsync(SyncTestData.CompletedTrack());

        var first = slow.HoldWhile(track.Id, () => StartAsync(factory, track.Id, AnalysisKind.Chords));
        await slow.Entered.Task.WaitAsync(Patience, Ct);
        var second = StartAsync(factory, track.Id, AnalysisKind.Chords);
        await Task.Delay(50, Ct);
        second.IsCompleted.ShouldBeFalse("the same track and kind waits for the lock");
        slow.Release();

        (await Task.WhenAll(first, second)).Select(result => result.Outcome).ShouldBe([StartOutcome.Stored, StartOutcome.AnalysisRunning], ignoreOrder: true);
        factory.AnalysisWorker.Requests.Count.ShouldBe(1);
    }

    private static void SplitKickAndSnare(Track track)
    {
        track.RequestDetail(T0);
        track.AssignDetailJob(SyncTestData.DetailJobId, T0);
        track.CompleteDetail([new ParentOutcome("drums", ParentOutcomeKind.Split, [new("kick", 8), new("snare", 8), new("rest", 8)])], T0);
    }

    /// <summary>A drums split whose drum hits are ready, then the files removed and the split redone without drum stems.</summary>
    private static async Task<Track> ResplitWithoutDrumsAsync(ApiFactory factory)
    {
        var track = await factory.SeedAsync(SyncTestData.DetailCompleted());
        await factory.SeedReadyAsync(track.Id, new DrumHitsDocument { Engine = "drum-onsets", Pieces = [DrumPiece.Kick], Hits = [new DrumHit(500, DrumPiece.Kick, 0.9)] }, new string('1', 64));
        return await factory.UpdateTrackAsync(track.Id, changed =>
        {
            changed.RemoveDetailFiles(T0.AddHours(1));
            changed.RequestDetail(T0.AddHours(1));
            changed.AssignDetailJob(SyncTestData.DetailJobId, T0.AddHours(1));
            changed.CompleteDetail([new ParentOutcome("drums", ParentOutcomeKind.NotReliable, [])], T0.AddHours(2));
        });
    }

    /// <summary>Two queued chord analyses whose worker jobs completed; the first is older, so the pass reads it first.</summary>
    private async Task<(Guid First, Guid Second)> TwoCompletedChordJobsAsync()
    {
        var first = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        var second = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        await Factory.SeedQueuedAtAsync(first.Id, OlderJob, T0.AddMinutes(-10));
        await Factory.SeedQueuedAtAsync(second.Id, NewerJob, T0);
        foreach (var jobId in new[] { OlderJob, NewerJob })
        {
            Factory.AnalysisWorker.Jobs[jobId] = new WorkerAnalysisJob { AnalysisJobId = jobId, Kind = "chords", Status = WorkerPracticeJobState.Queued };
            Factory.AnalysisWorker.Complete(jobId);
        }

        return (first.Id, second.Id);
    }

    /// <summary>Holds the fingerprint of one track until released (a slow SHA-256 over large stems).</summary>
    private sealed class SlowFingerprinter
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid _blocked;

        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<T> HoldWhile<T>(Guid trackId, Func<Task<T>> action)
        {
            _blocked = trackId;
            return action();
        }

        public void Release() => _release.TrySetResult();

        public IStemFingerprinter Wrap(IStemFingerprinter inner) => new Slow(this, inner);

        private sealed class Slow(SlowFingerprinter owner, IStemFingerprinter inner) : IStemFingerprinter
        {
            public async Task<string> FingerprintAsync(Guid trackId, IReadOnlyList<RenderChannel> inputs, CancellationToken cancellationToken)
            {
                if (trackId == owner._blocked)
                {
                    owner.Entered.TrySetResult();
                    await owner._release.Task.WaitAsync(cancellationToken);
                }

                return await inner.FingerprintAsync(trackId, inputs, cancellationToken);
            }
        }
    }
}
