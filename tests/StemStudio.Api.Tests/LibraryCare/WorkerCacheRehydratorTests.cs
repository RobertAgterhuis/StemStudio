using System.Net;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.LibraryCare;

/// <summary>013 T050: a worker cache miss on detail start or lyrics alignment rehydrates the job and retries once (FR-019).</summary>
public sealed class WorkerCacheRehydratorTests : ApiTestBase
{
    private const string Rehydrated = "5d4c3b2a1f0e4d9c8b7a6f5e4d3c2b1a";
    private const string DetailJob = "7c1e9a2b4d6f48e0a3b5c7d9e1f20a4b";

    [Fact]
    public async Task DetailStart_OnACacheMiss_RehydratesAndRetriesWithTheNewJob()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        var original = track.WorkerJobId!;
        Worker.OnCreateDetail = standard => standard == original
            ? throw new WorkerRequestException(HttpStatusCode.Conflict, "Stems are no longer in the worker cache.")
            : Task.FromResult(FakeWorkerClient.DetailJob(DetailJob, WorkerDetailJobState.Queued));
        Worker.OnRehydrate = _ => Task.FromResult(new WorkerRehydratedJob(Rehydrated, new string('a', 64), "completed", "htdemucs_6s", [], ApiFactory.Start));

        await Factory.SyncDetailAsync();

        var saved = (await Factory.FindTrackAsync(track.Id))!;
        (saved.WorkerJobId, saved.DetailPhase!.WorkerDetailJobId, saved.DetailPhase.Status).ShouldBe((Rehydrated, DetailJob, DetailPhaseStatus.Queued));
        var request = Worker.RehydrateRequests.ShouldHaveSingleItem();
        (request.TrackId, request.SourceType, request.SourceKey, request.Model, request.Stems.Count).ShouldBe((track.Id, "youtube", "youtube:dQw4w9WgXcQ", "htdemucs_6s", 6));
    }

    [Fact]
    public async Task DetailStart_SecondFailure_SurfacesTheOriginalError()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = standard => throw new WorkerRequestException(HttpStatusCode.Conflict, standard == Rehydrated ? "second" : "Stems are gone.");
        Worker.OnRehydrate = _ => Task.FromResult(new WorkerRehydratedJob(Rehydrated, new string('a', 64), "completed", "htdemucs_6s", [], ApiFactory.Start));

        await Factory.SyncDetailAsync();

        var phase = (await Factory.FindTrackAsync(track.Id))!.DetailPhase!;
        (phase.Status, phase.Error).ShouldBe((DetailPhaseStatus.Failed, "Stems are gone."));
    }

    [Fact]
    public async Task DetailStart_FailedRehydration_SurfacesTheOriginalError()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "Unknown standard job.");
        Worker.OnRehydrate = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "stem missing");

        await Factory.SyncDetailAsync();

        (await Factory.FindTrackAsync(track.Id))!.DetailPhase!.Error.ShouldBe("Unknown standard job.");
    }

    [Fact]
    public async Task OtherRejections_AreNotRehydrated()
    {
        await Factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));
        Worker.OnCreateDetail = _ => throw new WorkerRequestException(HttpStatusCode.UnprocessableEntity, "invalid");

        await Factory.SyncDetailAsync();

        Worker.RehydrateRequests.ShouldBeEmpty();
    }

    [Fact]
    public void Request_ForUploadsAndSpotifyUsesTheSourceType()
    {
        var track = SyncTestData.CompletedTrack();

        var request = WorkerCacheRehydrator.Request(track);

        (request.SourceType, request.Stems).ShouldBe(("youtube", track.Stems.Select(stem => stem.Name).ToList()));
        WorkerCacheRehydrator.IsCacheMiss(new WorkerRequestException(HttpStatusCode.Conflict, "x")).ShouldBeTrue();
        WorkerCacheRehydrator.IsCacheMiss(new WorkerRequestException(HttpStatusCode.BadRequest, "x")).ShouldBeFalse();
    }
}
