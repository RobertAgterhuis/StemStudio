using System.Net;
using StemStudio.Api.InstrumentViews;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.InstrumentViews;
using StemStudio.Domain.InstrumentViews;
using static StemStudio.Api.Tests.InstrumentViews.AnalysisTestData;

namespace StemStudio.Api.Tests.InstrumentViews;

/// <summary>Feature 016 T086: the drum hits operations <c>getDrumHits</c>, <c>detectDrumHits</c>, <c>deleteDrumHits</c> (FR-030).</summary>
public sealed class DrumHitEndpointsTests : ApiTestBase
{
    private FakeWorkerAnalysisClient Analysis => Factory.AnalysisWorker;

    private static DrumHitsDocument Hits() => new()
    {
        Engine = "drum-onsets",
        Pieces = [DrumPiece.Kick, DrumPiece.Snare],
        Hits = [new DrumHit(500, DrumPiece.Kick, 0.9), new DrumHit(1000, DrumPiece.Snare, 0.6)],
    };

    private static Uri Url(string route) => new(route, UriKind.Relative);

    private Task<HttpResponseMessage> PostAsync(Guid trackId) => Client.PostAsync(Url(ApiRoutes.DrumHits(trackId)), null, Ct);

    private async Task<TrackAnalysisDto<DrumHitsDocumentDto>> GetAsync(Guid trackId)
    {
        using var response = await Client.GetAsync(Url(ApiRoutes.DrumHits(trackId)), Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        await ContractAssert.MatchesAsync(response, "getDrumHits");
        return await response.BodyAsync<TrackAnalysisDto<DrumHitsDocumentDto>>();
    }

    [Fact]
    public async Task Get_UnknownTrack_Is404()
    {
        using var response = await Client.GetAsync(Url(ApiRoutes.DrumHits(Guid.NewGuid())), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ContractAssert.MatchesAsync(response, "getDrumHits");
    }

    [Fact]
    public async Task Get_WithoutASplit_IsUnavailable_SplitTheDrumsFirst()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        var dto = await GetAsync(track.Id);

        (dto.Kind, dto.Status, dto.Reason, dto.Document, dto.Limits).ShouldBe((AnalysisKindDto.DrumHits, AnalysisStatusDto.Unavailable, "Split the drums first.", null, InstrumentViewMapper.DrumHitLimits));
    }

    [Fact]
    public async Task Detect_WithoutASplit_Is409DrumsNotSplit()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());

        using var response = await PostAsync(track.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await ContractAssert.MatchesAsync(response, "detectDrumHits");
        (await response.JsonAsync()).GetProperty("type").GetString().ShouldBe($"https://stemstudio.local/problems/{InstrumentViewProblemTypes.DrumsNotSplit}");
        Analysis.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Detect_Queues_ThenTheHitsAreReady()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());

        using var queued = await PostAsync(track.Id);

        queued.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(queued, "detectDrumHits");
        (await queued.BodyAsync<TrackAnalysisDto<DrumHitsDocumentDto>>()).Status.ShouldBe(AnalysisStatusDto.Queued);
        Analysis.Requests.Single().ShouldBeOfType<WorkerDrumHitsRequest>();

        Analysis.Complete(Analysis.Jobs.Keys.Single(), FakeWorkerAnalysisClient.DrumHits());
        await Factory.SyncAnalysesAsync();

        var dto = await GetAsync(track.Id);
        (dto.Status, dto.Engine).ShouldBe((AnalysisStatusDto.Ready, AnalysisEngineDto.DrumOnsets));
        dto.Document!.Hits.Select(hit => (hit.TimeMs, hit.Piece)).ShouldBe([(500, DrumPieceDto.Kick), (1000, DrumPieceDto.Snare)]);
        dto.Document.Pieces.ShouldBe([DrumPieceDto.Kick, DrumPieceDto.Snare]);
    }

    [Fact]
    public async Task Detect_RefusesARunningAnalysisAndADownWorker_And404ForAnUnknownTrack()
    {
        var running = await Factory.SeedAsync(SyncTestData.DetailCompleted("aaaaaaaaaaa"));
        await Factory.SeedQueuedAsync(running.Id, AnalysisKind.DrumHits, JobId);

        using var busy = await PostAsync(running.Id);
        busy.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await busy.JsonAsync()).GetProperty("type").GetString().ShouldBe($"https://stemstudio.local/problems/{InstrumentViewProblemTypes.AnalysisRunning}");
        using var unknown = await PostAsync(Guid.NewGuid());
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        Analysis.Unavailable = true;
        var fresh = await Factory.SeedAsync(SyncTestData.DetailCompleted("bbbbbbbbbbb"));
        using var down = await PostAsync(fresh.Id);
        down.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        await ContractAssert.MatchesAsync(down, "detectDrumHits");
    }

    [Fact]
    public async Task TheDrumsSplit_StartsTheHitsAutomatically()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailQueued());
        Factory.Worker.OnGetDetailJob = id => Task.FromResult<WorkerDetailJob?>(FakeWorkerClient.DetailJob(id, WorkerDetailJobState.Completed, 100));

        await Factory.SyncDetailAsync();
        await Factory.SyncDetailAsync();

        (await GetAsync(track.Id)).Status.ShouldBe(AnalysisStatusDto.Queued);
    }

    [Fact]
    public async Task ANewSplit_MakesReadyHitsStale_AndTheyStillShow()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Factory.SeedReadyAsync(track.Id, Hits(), new string('1', 64));
        var stored = (await Factory.FindTrackAsync(track.Id))!;
        await Factory.WithServiceAsync<bool, TrackAnalysisService>(service => service.RefreshStalenessAsync(stored, AnalysisKind.DrumHits, Ct));

        var dto = await GetAsync(track.Id);

        (dto.Status, dto.Reason, dto.Document!.Hits.Count).ShouldBe((AnalysisStatusDto.Stale, TrackAnalysis.StaleReason, 2));
    }

    [Fact]
    public async Task Delete_CancelsAndClears_And404ForAnUnknownTrack()
    {
        var track = await Factory.SeedAsync(SyncTestData.DetailCompleted());
        await Factory.SeedQueuedAsync(track.Id, AnalysisKind.DrumHits, JobId);

        using var deleted = await Client.DeleteAsync(Url(ApiRoutes.DrumHits(track.Id)), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await ContractAssert.MatchesAsync(deleted, "deleteDrumHits");
        Analysis.Deleted.ShouldBe([JobId]);
        (await GetAsync(track.Id)).Status.ShouldBe(AnalysisStatusDto.None);
        using var unknown = await Client.DeleteAsync(Url(ApiRoutes.DrumHits(Guid.NewGuid())), Ct);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
