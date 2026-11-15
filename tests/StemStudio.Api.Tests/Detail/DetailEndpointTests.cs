using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Storage;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Domain.Tracks.Detail;

namespace StemStudio.Api.Tests.Detail;

/// <summary>Start, cancel and the delete rule of the detail phase (T067; FR-008, FR-009, FR-016).</summary>
public sealed class DetailEndpointTests : IDisposable
{
    private const string DetailJobId = SyncTestData.DetailJobId;
    private readonly ApiFactory _factory = new();
    private readonly HttpClient _client;

    public DetailEndpointTests() => _client = _factory.CreateClient();

    private FakeWorkerClient Worker => _factory.Worker;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Start_FromNoneQueuesThePhaseWithoutCallingTheWorker()
    {
        var track = await _factory.SeedAsync(SyncTestData.CompletedTrack());
        _factory.Clock.Advance(TimeSpan.FromMinutes(5));

        using var response = await PostAsync(ApiRoutes.StartDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        response.Headers.Location!.OriginalString.ShouldBe(ApiRoutes.Track(track.Id));
        var dto = await response.BodyAsync<TrackDto>();
        (dto.Detail.Status, dto.Detail.Stage, dto.Detail.Progress).ShouldBe((DetailPhaseStatusDto.Queued, "Queued", 0));
        dto.Detail.UpdatedAt.ShouldBe(ApiFactory.Start.AddMinutes(5));
        (await PhaseAsync(track)).WorkerDetailJobId.ShouldBeNull();
        Worker.DetailCreates.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "startDetail");
    }

    [Theory]
    [InlineData(DetailPhaseStatus.Failed)]
    [InlineData(DetailPhaseStatus.Cancelled)]
    public async Task Start_RetriesAFailedOrCancelledPhaseAndRemovesOldDetailFiles(DetailPhaseStatus status)
    {
        var track = await _factory.SeedAsync(Finished(status));
        await DetailFiles.SaveAsync(new DetailStemKey(track.Id, "drums", "kick"), (s, ct) => s.WriteAsync(new byte[4], ct).AsTask(), Ct);

        using var response = await PostAsync(ApiRoutes.StartDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var phase = await PhaseAsync(track);
        (phase.Status, phase.Progress, phase.Error, phase.WorkerDetailJobId).ShouldBe((DetailPhaseStatus.Queued, 0, null, null));
        DetailFiles.Find(new DetailStemKey(track.Id, "drums", "kick")).ShouldBeNull();
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("running")]
    [InlineData("completed")]
    public async Task Start_IsAConflictWhileActiveOrCompleted(string status)
    {
        var track = await _factory.SeedAsync(WithPhase(status));

        using var response = await PostAsync(ApiRoutes.StartDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString()!.ShouldContain(status);
        await ContractAssert.MatchesAsync(response, "startDetail");
    }

    [Fact]
    public async Task Start_IsAConflictWhenTheTrackIsNotCompleted()
    {
        var queued = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var failed = await _factory.SeedAsync(SyncTestData.FailedTrack("bbbbbbbbbbb"));

        using var notReady = await PostAsync(ApiRoutes.StartDetail(queued.Id));
        using var notCompleted = await PostAsync(ApiRoutes.StartDetail(failed.Id));

        (notReady.StatusCode, notCompleted.StatusCode).ShouldBe((HttpStatusCode.Conflict, HttpStatusCode.Conflict));
        (await notReady.JsonAsync()).GetProperty("detail").GetString().ShouldBe("Only completed tracks can be split further.");
        await ContractAssert.MatchesAsync(notCompleted, "startDetail");
    }

    [Fact]
    public async Task StartAndCancel_404ForAnUnknownTrack()
    {
        using var start = await PostAsync(ApiRoutes.StartDetail(Guid.NewGuid()));
        using var cancel = await PostAsync(ApiRoutes.CancelDetail(Guid.NewGuid()));

        (start.StatusCode, cancel.StatusCode).ShouldBe((HttpStatusCode.NotFound, HttpStatusCode.NotFound));
        await ContractAssert.MatchesAsync(start, "startDetail");
        await ContractAssert.MatchesAsync(cancel, "cancelDetail");
    }

    [Fact]
    public async Task Cancel_PendingSubmissionCancelsLocally()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued(detailJobId: null));

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.BodyAsync<TrackDto>()).Detail.Status.ShouldBe(DetailPhaseStatusDto.Cancelled);
        Worker.DetailCancels.ShouldBeEmpty();
        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Cancelled);
        await ContractAssert.MatchesAsync(response, "cancelDetail");
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("running")]
    public async Task Cancel_QueuedOrRunningCancelsTheWorkerJob(string status)
    {
        var track = await _factory.SeedAsync(WithPhase(status));

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Worker.DetailCancels.ShouldBe([DetailJobId]);
        var phase = await PhaseAsync(track);
        (phase.Status, phase.Stage).ShouldBe((DetailPhaseStatus.Cancelled, "Cancelled"));
        (await _factory.FindTrackAsync(track.Id))!.Status.ShouldBe(TrackStatus.Completed);
    }

    [Fact]
    public async Task Cancel_JobTheWorkerNoLongerKnowsCancelsLocally()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnCancelDetail = _ => throw new WorkerRequestException(HttpStatusCode.NotFound, "Detail job does not exist.");

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Cancelled);
    }

    [Fact]
    public async Task Cancel_JobThatAlreadyFinishedInTheWorkerIsAConflictAndChangesNothing()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnCancelDetail = _ => throw new WorkerRequestException(HttpStatusCode.Conflict, "Detail job is in state completed.");

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString().ShouldBe("Detail job is in state completed.");
        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Queued);
        await ContractAssert.MatchesAsync(response, "cancelDetail");
    }

    [Fact]
    public async Task Cancel_UnreachableWorkerIs503AndChangesNothing()
    {
        var track = await _factory.SeedAsync(SyncTestData.DetailQueued());
        Worker.OnCancelDetail = _ => throw new WorkerUnavailableException();

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await PhaseAsync(track)).Status.ShouldBe(DetailPhaseStatus.Queued);
        await ContractAssert.MatchesAsync(response, "cancelDetail");
    }

    [Theory]
    [InlineData("none")]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task Cancel_IsAConflictWithoutAnActivePhase(string status)
    {
        var track = await _factory.SeedAsync(WithPhase(status));

        using var response = await PostAsync(ApiRoutes.CancelDetail(track.Id));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        Worker.DetailCancels.ShouldBeEmpty();
        await ContractAssert.MatchesAsync(response, "cancelDetail");
    }

    [Theory]
    [InlineData("queued")]
    [InlineData("running")]
    public async Task Delete_IsRefusedWhileTheDetailPhaseIsActive(string status)
    {
        var track = await _factory.SeedAsync(WithPhase(status));

        using var response = await _client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.JsonAsync()).GetProperty("detail").GetString()!.ShouldContain("cancel it first");
        (await _factory.FindTrackAsync(track.Id)).ShouldNotBeNull();
        await ContractAssert.MatchesAsync(response, "deleteTrack");
    }

    [Theory]
    [InlineData("completed")]
    [InlineData("failed")]
    [InlineData("cancelled")]
    public async Task Delete_RemovesTheDetailFolderOtherwise(string status)
    {
        var track = await _factory.SeedAsync(WithPhase(status));
        await DetailFiles.SaveAsync(new DetailStemKey(track.Id, "drums", "kick"), (s, ct) => s.WriteAsync(new byte[4], ct).AsTask(), Ct);

        using var response = await _client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Directory.Exists(Path.Combine(_factory.DataDir, "tracks", track.Id.ToString(), "detail")).ShouldBeFalse();
    }

    private IDetailStemFileStore DetailFiles => _factory.Services.GetRequiredService<IDetailStemFileStore>();

    private static Track WithPhase(string status) => status switch
    {
        "none" => SyncTestData.CompletedTrack(),
        "queued" => SyncTestData.DetailQueued(),
        "running" => Running(),
        "completed" => SyncTestData.DetailCompleted(),
        "failed" => Finished(DetailPhaseStatus.Failed),
        _ => Finished(DetailPhaseStatus.Cancelled),
    };

    private static Track Running()
    {
        var track = SyncTestData.DetailQueued();
        track.ApplyDetailProgress(new DetailProgress(DetailPhaseStatus.Running, "Splitting drums (1 of 3)", "drums", 12), SyncTestData.T0);
        return track;
    }

    private static Track Finished(DetailPhaseStatus status)
    {
        var track = Running();
        if (status == DetailPhaseStatus.Failed)
        {
            track.FailDetail("interrupted by restart", SyncTestData.T0);
        }
        else
        {
            track.CancelDetail(SyncTestData.T0);
        }

        return track;
    }

    private async Task<DetailPhase> PhaseAsync(Track track) => (await _factory.FindTrackAsync(track.Id))!.DetailPhase!;

    private Task<HttpResponseMessage> PostAsync(string path) => _client.PostAsync(new Uri(path, UriKind.Relative), null, Ct);
}
