using System.Net;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Detail;

/// <summary>
/// A cancel that races the detail synchroniser is never overwritten (E2E agent us2-detail, main run 44212916383): the pass
/// works on tracks it loaded at its start and must not save a stale phase over a newer one.
/// </summary>
public sealed class DetailConcurrencyTests : ApiTestBase
{
    private const string FirstStandardJob = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SecondStandardJob = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string SubmittedJob = "cccccccccccccccccccccccccccccccc";

    [Fact]
    public async Task CancelAfterThePassLoadedTheTrack_IsNotOverwrittenBySubmission()
    {
        var first = await Factory.SeedAsync(Pending("aaaaaaaaaaa", FirstStandardJob));
        var second = await Factory.SeedAsync(Pending("bbbbbbbbbbb", SecondStandardJob));
        var cancelled = new Dictionary<string, Guid> { [FirstStandardJob] = second.Id, [SecondStandardJob] = first.Id };
        Guid? victim = null;
        Worker.OnCreateDetail = async standard =>
        {
            if (victim is null)
            {
                // While the pass submits its first track, the user cancels the other one (still pending, cancelled locally).
                victim = cancelled[standard];
                using var cancel = await PostAsync(ApiRoutes.CancelDetail(victim.Value));
                cancel.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            return FakeWorkerClient.DetailJob(SubmittedJob, WorkerDetailJobState.Queued);
        };

        await Factory.SyncDetailAsync();

        (await PhaseAsync(victim!.Value)).Status.ShouldBe(DetailPhaseStatus.Cancelled);
        Worker.DetailCreates.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CancelDuringTheSubmission_CancelsTheSubmittedWorkerJob()
    {
        var track = await Factory.SeedAsync(Pending("aaaaaaaaaaa", FirstStandardJob));
        Task<HttpResponseMessage>? cancel = null;
        Worker.OnCreateDetail = async _ =>
        {
            // The cancel arrives while the worker call is in flight; give it time to finish when nothing holds it back.
            cancel = PostAsync(ApiRoutes.CancelDetail(track.Id));
            await Task.WhenAny(cancel, Task.Delay(TimeSpan.FromSeconds(1), Ct));
            return FakeWorkerClient.DetailJob(SubmittedJob, WorkerDetailJobState.Queued);
        };

        await Factory.SyncDetailAsync();
        using var response = await cancel!;

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PhaseAsync(track.Id)).Status.ShouldBe(DetailPhaseStatus.Cancelled);
        Worker.DetailCancels.ShouldBe([SubmittedJob]);
    }

    private static Track Pending(string videoId, string standardJobId)
    {
        var track = SyncTestData.DetailQueued(videoId, detailJobId: null);
        track.RehydrateWorkerJob(standardJobId, SyncTestData.T0);
        return track;
    }

    private async Task<DetailPhase> PhaseAsync(Guid id) => (await Factory.FindTrackAsync(id))!.DetailPhase!;

    private Task<HttpResponseMessage> PostAsync(string path) => Client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);
}
