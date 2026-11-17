using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.ScoreFollow;
using StemStudio.Api.Tests.Assets;
using StemStudio.Api.Tests.Contract;
using StemStudio.Api.Tests.Scores;
using StemStudio.Api.Tests.SongSections;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Tests.Worker;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Assets;
using StemStudio.Contracts.ScoreFollow;
using StemStudio.Contracts.Scores;
using StemStudio.Domain.Practice;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.ScoreFollow;

/// <summary>Feature 014 T078: score list per track, score views, their pages and the automatic view of a new score (FR-009, FR-014).</summary>
public sealed class ScoreFollowEndpointsTests : ApiTestBase
{
    private FakeWorkerPracticeClient Practice => Factory.PracticeWorker;

    [Fact]
    public async Task NewScore_GetsItsViewQueuedAutomatically()
    {
        var (_, score) = await ReadyChannelScoreAsync();

        var view = (await FindViewAsync(score.Id))!;

        (view.Status, view.OffsetSeconds).ShouldBe((ScoreViewStatus.Queued, 0.0));
        var sent = Practice.ScoreViewRequests.Single();
        sent.Musicxml.ShouldBe($"assets/{score.Id:D}/score.musicxml");
        sent.Grid.ShouldBeOfType<WorkerBeatGridTempo>().Downbeats.ShouldBe([0.52]);
        sent.DurationSeconds.ShouldBe(213.04);
    }

    [Fact]
    public async Task AFailingReadyHook_DoesNotFailTheScore()
    {
        using var factory = new ApiFactory { ConfigureServices = services => services.AddScoped<StemStudio.Api.Sync.IAssetReadyHook, ThrowingHook>() };
        using var client = factory.CreateClient();
        var track = await factory.SeedAsync(AssetTestData.NamedTrack());
        var score = await (await client.PostScoreAsync(ScoreTestData.Request(track.Id, "bass"))).BodyAsync<AssetDto>();
        ScoreTestData.CompleteTranscriptions(factory.Worker);

        await factory.SyncAssetsAsync();

        var stored = await (await client.GetAsync(new Uri(ApiRoutes.Asset(score.Id), UriKind.Relative), Ct)).BodyAsync<AssetDto>();
        stored.Status.ShouldBe(AssetStatusDto.Ready);
        factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("ready hook failed", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListTrackScores_ListsDirectAndExportedScores_ButNotOtherTracks()
    {
        var (track, direct) = await ReadyChannelScoreAsync();
        var fromExport = await ReadyExportScoreAsync(track.Id);
        var other = await Factory.SeedAsync(AssetTestData.NamedTrack("aaaaaaaaaaa"));
        await ReadyScoreAsync(ScoreTestData.Request(other.Id, "bass"));

        using var response = await Client.GetAsync(new Uri(ApiRoutes.TrackScores(track.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "listTrackScores");
        var scores = await response.BodyAsync<List<TrackScoreDto>>();
        scores.Select(score => score.AssetId).ShouldBe([direct.Id, fromExport.Id], ignoreOrder: true);
        var exported = scores.Single(score => score.AssetId == fromExport.Id);
        (exported.Source, exported.RangeStartSeconds, exported.Profile, exported.ViewStatus).ShouldBe(("asset", (double?)2.5, ScoreProfileDto.Bass, ScoreViewStatusDto.Queued));
        var own = scores.Single(score => score.AssetId == direct.Id);
        (own.Source, own.RangeStartSeconds, own.ManualTempo).ShouldBe(("bass", (double?)null, false));
    }

    [Fact]
    public async Task ListTrackScores_UnknownTrack_Is404()
    {
        using var response = await Client.GetAsync(new Uri(ApiRoutes.TrackScores(Guid.NewGuid()), UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Applier_StoresPagesAndBarsShiftedByTheRangeOffset()
    {
        var (track, _) = await ReadyChannelScoreAsync();
        var score = await ReadyScoreAsync(ScoreTestData.Request(track.Id, "bass") with { Range = new RangeDto(10, 40) });
        var jobId = (await FindViewAsync(score.Id))!.WorkerJobId!;
        Practice.CompleteScoreView(jobId);

        await SyncViewsAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreView(score.Id), UriKind.Relative), Ct);
        await ContractAssert.MatchesAsync(response, "getScoreView");
        var view = await response.BodyAsync<ScoreViewDto>();
        (view.Status, view.OffsetSeconds, view.TrackId).ShouldBe((ScoreViewStatusDto.Ready, 10.0, (Guid?)track.Id));
        view.Bars.Select(bar => bar.StartSeconds).ShouldBe([10, 12.1, 14.1], 1e-9);
        view.Pages.Single().ShouldBe(new ScoreViewPageDto { Page = 1, Url = $"{ApiRoutes.ScoreViewPage(score.Id, 1)}?v=1", WidthPx = 840, HeightPx = 1188 });
        Practice.ScoreViewRequests.Last().DurationSeconds.ShouldBe(30);
        Practice.Deleted.ShouldContain(jobId);
        File.Exists(Path.Combine(Factory.DataDir, "assets", score.Id.ToString("D"), "view", "page-001.svg")).ShouldBeTrue();
    }

    [Fact]
    public async Task Applier_ScoreOfAnExportedRange_AddsBothOffsets()
    {
        var (track, _) = await ReadyChannelScoreAsync();
        var score = await ReadyExportScoreAsync(track.Id, new RangeDto(1, 20));
        Practice.CompleteScoreView((await FindViewAsync(score.Id))!.WorkerJobId!);

        await SyncViewsAsync();

        var view = (await FindViewAsync(score.Id))!;
        view.OffsetSeconds.ShouldBe(3.5);
        view.Bars[0].StartSeconds.ShouldBe(3.5, 1e-9);
    }

    [Fact]
    public async Task Applier_RunningThenFailed_StoresTheReason()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        var jobId = (await FindViewAsync(score.Id))!.WorkerJobId!;
        Practice.SetScoreView(jobId, WorkerPracticeJobState.Running);
        await SyncViewsAsync();
        (await FindViewAsync(score.Id))!.Status.ShouldBe(ScoreViewStatus.Rendering);
        Practice.SetScoreView(jobId, WorkerPracticeJobState.Failed, "Verovio could not read the score.");

        await SyncViewsAsync();

        var view = (await FindViewAsync(score.Id))!;
        (view.Status, view.Error).ShouldBe((ScoreViewStatus.Failed, "Verovio could not read the score."));
        Practice.Deleted.ShouldContain(jobId);
    }

    [Fact]
    public async Task Applier_LostJob_AndAnInvalidResult_Fail()
    {
        var (track, lost) = await ReadyChannelScoreAsync();
        var invalid = await ReadyScoreAsync(ScoreTestData.Request(track.Id, "guitar"));
        Practice.ScoreViewJobs.TryRemove((await FindViewAsync(lost.Id))!.WorkerJobId!, out _);
        Practice.CompleteScoreView((await FindViewAsync(invalid.Id))!.WorkerJobId!, new WorkerScoreViewResult([], [], new WorkerAnalyzer("verovio", "6.4.0")));

        await SyncViewsAsync();

        (await FindViewAsync(lost.Id))!.Error.ShouldBe(ScoreViewJobApplier.JobLost);
        (await FindViewAsync(invalid.Id))!.Error.ShouldBe(ScoreViewJobApplier.InvalidResult);
    }

    [Fact]
    public async Task Applier_WorkerDown_ChangesNothing()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        Practice.Unavailable = true;

        await SyncViewsAsync();

        (await FindViewAsync(score.Id))!.Status.ShouldBe(ScoreViewStatus.Queued);
    }

    [Fact]
    public async Task GetScoreView_ReadyScoreWithoutAViewRow_IsStatusNone_AndCanBeMadeLazily()
    {
        // As after a (merge) restore: backups do not archive assets/<id>/view/ and bring no ScoreViews row (014 T107 decision).
        Practice.Unavailable = true;
        var (_, score) = await ReadyChannelScoreAsync();
        (await FindViewAsync(score.Id)).ShouldBeNull();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreView(score.Id), UriKind.Relative), Ct);

        await ContractAssert.MatchesAsync(response, "getScoreView");
        var view = await response.BodyAsync<ScoreViewDto>();
        (view.Status, view.Pages.Count, view.Bars.Count).ShouldBe((ScoreViewStatusDto.None, 0, 0));
        Practice.Unavailable = false;
        using var made = await PostViewAsync(score.Id);
        (await made.BodyAsync<ScoreViewDto>()).Status.ShouldBe(ScoreViewStatusDto.Queued);
    }

    [Fact]
    public async Task CreateScoreView_IsIdempotent_AndQueuesOlderScores()
    {
        Practice.Unavailable = true;
        var (_, score) = await ReadyChannelScoreAsync();
        using (var down = await PostViewAsync(score.Id))
        {
            await SectionEndpointsTests.AssertProblemAsync(down, HttpStatusCode.ServiceUnavailable, "worker-unavailable");
        }

        Practice.Unavailable = false;

        using var first = await PostViewAsync(score.Id);
        using var second = await PostViewAsync(score.Id);

        first.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        await ContractAssert.MatchesAsync(first, "createScoreView");
        (await second.BodyAsync<ScoreViewDto>()).Status.ShouldBe(ScoreViewStatusDto.Queued);
        Practice.ScoreViewRequests.Count.ShouldBe(1);
    }

    [Fact]
    public async Task CreateScoreView_AfterAFailure_SubmitsAgain()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        Practice.SetScoreView((await FindViewAsync(score.Id))!.WorkerJobId!, WorkerPracticeJobState.Failed, "x");
        await SyncViewsAsync();

        using var response = await PostViewAsync(score.Id);

        (await response.BodyAsync<ScoreViewDto>()).Status.ShouldBe(ScoreViewStatusDto.Queued);
        Practice.ScoreViewRequests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task CreateScoreView_ConflictsAndUnknownItems()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        var audio = await ReadyExportAsync(AssetTestData.Raw(track.Id, "bass"));
        var pending = await (await Client.PostScoreAsync(ScoreTestData.Request(track.Id, "bass"))).BodyAsync<AssetDto>();

        using var notAScore = await PostViewAsync(audio.Id);
        using var notReady = await PostViewAsync(pending.Id);
        using var unknown = await PostViewAsync(Guid.NewGuid());
        using var getAudio = await Client.GetAsync(new Uri(ApiRoutes.ScoreView(audio.Id), UriKind.Relative), Ct);

        await SectionEndpointsTests.AssertProblemAsync(notAScore, HttpStatusCode.Conflict, "not-a-score");
        await ContractAssert.MatchesAsync(notAScore, "createScoreView");
        await SectionEndpointsTests.AssertProblemAsync(notReady, HttpStatusCode.Conflict, "score-not-ready");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await SectionEndpointsTests.AssertProblemAsync(getAudio, HttpStatusCode.Conflict, "not-a-score");
    }

    [Fact]
    public async Task ScoreWithAManualTempo_SendsTheManualGrid()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());

        var score = await ReadyScoreAsync(ScoreTestData.Request(track.Id, "bass") with { Tempo = new ScoreTempoDto(96, 3, 0.4) });

        Practice.ScoreViewRequests.Single().Grid.ShouldBe(new WorkerManualTempo(96, 3, 0.4));
        using var list = await Client.GetAsync(new Uri(ApiRoutes.TrackScores(track.Id), UriKind.Relative), Ct);
        (await list.BodyAsync<List<TrackScoreDto>>()).Single(item => item.AssetId == score.Id).ManualTempo.ShouldBeTrue();
    }

    [Fact]
    public async Task Page_IsServedAsAnImage_WithTheSecurityHeaders()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        Practice.CompleteScoreView((await FindViewAsync(score.Id))!.WorkerJobId!);
        await SyncViewsAsync();

        using var response = await Client.GetAsync(new Uri(ApiRoutes.ScoreViewPage(score.Id, 1) + "?v=1", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldBe(ScoreFollowEndpoints.PageSecurityPolicy);
        response.Headers.CacheControl!.ToString().ShouldContain("immutable");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe(Practice.PageSvg);
    }

    [Fact]
    public async Task Page_BeforeReady_Is409_AndOutOfRange_Is404()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        using (var early = await Client.GetAsync(new Uri(ApiRoutes.ScoreViewPage(score.Id, 1), UriKind.Relative), Ct))
        {
            await SectionEndpointsTests.AssertProblemAsync(early, HttpStatusCode.Conflict, "score-view-not-ready");
            await ContractAssert.MatchesAsync(early, "getScoreViewPage");
        }

        Practice.CompleteScoreView((await FindViewAsync(score.Id))!.WorkerJobId!);
        await SyncViewsAsync();

        using var beyond = await Client.GetAsync(new Uri(ApiRoutes.ScoreViewPage(score.Id, 2), UriKind.Relative), Ct);
        using var unknown = await Client.GetAsync(new Uri(ApiRoutes.ScoreViewPage(Guid.NewGuid(), 1), UriKind.Relative), Ct);
        beyond.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeletingTheScore_RemovesTheViewAndItsFiles()
    {
        var (_, score) = await ReadyChannelScoreAsync();
        Practice.CompleteScoreView((await FindViewAsync(score.Id))!.WorkerJobId!);
        await SyncViewsAsync();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Asset(score.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await FindViewAsync(score.Id)).ShouldBeNull();
        Directory.Exists(Path.Combine(Factory.DataDir, "assets", score.Id.ToString("D"), "view")).ShouldBeFalse();
    }

    [Fact]
    public async Task CreateScoreView_ForAScoreWhoseTrackIsGone_FailsWithTheNoGridText()
    {
        Practice.Unavailable = true;
        var (track, score) = await ReadyChannelScoreAsync();
        Practice.Unavailable = false;
        (await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct)).Dispose();

        using var response = await PostViewAsync(score.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var view = await response.BodyAsync<ScoreViewDto>();
        (view.Status, view.Error).ShouldBe((ScoreViewStatusDto.Failed, ScoreViewService.NoGrid));
        Practice.ScoreViewRequests.ShouldBeEmpty();
    }

    [Fact]
    public async Task QueueNew_IgnoresOtherItems_AndScoresThatHaveAView()
    {
        var (track, score) = await ReadyChannelScoreAsync();
        var audio = await ReadyExportAsync(AssetTestData.Raw(track.Id, "bass"));

        await Factory.WithScopeAsync(async sp =>
        {
            var assets = sp.GetRequiredService<IScoreViewRepository>();
            var found = await assets.AssetsAsync([audio.Id, score.Id], CancellationToken.None);
            var service = sp.GetRequiredService<ScoreViewService>();
            await service.QueueNewAsync(found[audio.Id], CancellationToken.None);
            await service.QueueNewAsync(found[score.Id], CancellationToken.None);
            return true;
        });

        Practice.ScoreViewRequests.Count.ShouldBe(1);
        (await FindViewAsync(audio.Id)).ShouldBeNull();
    }

    [Fact]
    public async Task Planner_TrackOf_FallsBackToTheRecipeAndTheAudioItem()
    {
        var (track, score) = await ReadyChannelScoreAsync();
        var fromExport = await ReadyExportScoreAsync(track.Id);

        var plans = await Factory.WithScopeAsync(async sp =>
        {
            var found = await sp.GetRequiredService<IScoreViewRepository>().AssetsAsync([score.Id, fromExport.Id], CancellationToken.None);
            return found.Values.Select(asset => ScoreViewPlanner.Plan(new ScoreSources(asset, null, null))).ToList();
        });

        plans.ShouldAllBe(plan => plan != null && plan.TrackId == track.Id && plan.Grid == null);
    }

    private Task<HttpResponseMessage> PostViewAsync(Guid assetId) => Client.PostAsync(new Uri(ApiRoutes.ScoreView(assetId), UriKind.Relative), null, Ct);

    private Task<ScoreView?> FindViewAsync(Guid assetId) =>
        Factory.WithScopeAsync(sp => sp.GetRequiredService<IScoreViewRepository>().FindAsync(assetId, CancellationToken.None));

    private Task<bool> SyncViewsAsync() => Factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<ScoreViewJobApplier>().SyncAllAsync(CancellationToken.None);
        return true;
    });

    private async Task<(Domain.Tracks.Track Track, AssetDto Score)> ReadyChannelScoreAsync()
    {
        var track = await Factory.SeedAsync(AssetTestData.NamedTrack());
        return (track, await ReadyScoreAsync(ScoreTestData.Request(track.Id, "bass")));
    }

    private async Task<AssetDto> ReadyScoreAsync(CreateScoreRequest request)
    {
        var score = await (await Client.PostScoreAsync(request)).BodyAsync<AssetDto>();
        ScoreTestData.CompleteTranscriptions(Worker);
        await Factory.SyncAssetsAsync();
        return score;
    }

    private async Task<AssetDto> ReadyExportScoreAsync(Guid trackId, RangeDto? scoreRange = null)
    {
        var audio = await ReadyExportAsync(AssetTestData.Raw(trackId, "bass") with { Range = new RangeDto(2.5, 200) });
        return await ReadyScoreAsync(new CreateScoreRequest { Source = AudioSourceDto.FromAsset(audio.Id), Range = scoreRange });
    }

    private async Task<AssetDto> ReadyExportAsync(CreateAudioExportRequest request)
    {
        var asset = await (await Client.PostExportAsync(request)).BodyAsync<AssetDto>();
        AssetTestData.CompleteRenders(Worker);
        await Factory.SyncAssetsAsync();
        return asset;
    }

    private sealed class ThrowingHook : StemStudio.Api.Sync.IAssetReadyHook
    {
        public Task OnReadyAsync(Domain.Assets.Asset asset, CancellationToken cancellationToken) => throw new InvalidOperationException("hook broke");
    }
}
