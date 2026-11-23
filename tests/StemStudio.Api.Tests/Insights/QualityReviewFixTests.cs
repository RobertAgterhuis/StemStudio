using System.Net;
using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Api.Worker;
using StemStudio.Contracts;
using StemStudio.Contracts.Insights;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Tests.Insights;

/// <summary>
/// Independent code review of feature 020 (quality reports): one broken job never stops the pass (3), only an unreachable
/// worker does, and deleting a track removes its running worker job (8).
/// </summary>
public sealed class QualityReviewFixTests : ApiTestBase
{
    public static TheoryData<string> BrokenPolls() => ["server-error", "refused", "unreadable"];

    [Theory]
    [MemberData(nameof(BrokenPolls))]
    public async Task ABrokenJob_DoesNotStopThePass(string how)
    {
        var (first, second) = await TwoRunningReportsAsync();
        var broken = (string?)null;
        Factory.InsightsWorker.BeforeGetQuality = job =>
        {
            broken ??= job;
            return job == broken ? Task.FromException(Failure(how)) : Task.CompletedTask;
        };
        CompleteAll();

        await Factory.SyncQualityAsync();

        var statuses = new[] { (await Client.GetQualityAsync(first)).Status, (await Client.GetQualityAsync(second)).Status };
        statuses.Count(status => status == QualityReportStatusDto.Ready).ShouldBe(1);
        statuses.Count(status => status == QualityReportStatusDto.Queued).ShouldBe(1, "the broken one is polled again on the next tick");
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("could not be synchronised", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ATrackDeletedDuringThePoll_DoesNotStopThePass()
    {
        var (first, second) = await TwoRunningReportsAsync();
        var jobs = new Dictionary<string, Guid>(StringComparer.Ordinal) { [JobOf(0)] = first, [JobOf(1)] = second };
        var deleted = (Guid?)null;
        Factory.InsightsWorker.BeforeGetQuality = async job =>
        {
            if (deleted is null)
            {
                deleted = jobs[job];
                await DeleteRowAsync(jobs[job]);
            }
        };
        CompleteAll();

        await Factory.SyncQualityAsync();

        var kept = deleted == first ? second : first;
        (await Client.GetQualityAsync(kept)).Status.ShouldBe(QualityReportStatusDto.Ready);
        (await Factory.FindTrackAsync(deleted!.Value)).ShouldBeNull();
    }

    [Fact]
    public async Task AnUnreachableWorker_StopsThePass()
    {
        await TwoRunningReportsAsync();
        Factory.InsightsWorker.BeforeGetQuality = _ => Task.FromException(new WorkerUnavailableException("down"));

        await Factory.SyncQualityAsync();

        Factory.InsightsWorker.Calls.Count(call => call.StartsWith("get-quality", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Fact]
    public async Task DeletingATrack_RemovesItsRunningQualityJob()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        var job = Factory.InsightsWorker.OnlyJob();

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.InsightsWorker.Deleted.ShouldBe([$"quality:{job}"]);
    }

    [Fact]
    public async Task DeletingATrack_WhenTheWorkerIsDown_StillDeletesIt()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        (await Client.RequestQualityAsync(track.Id)).Dispose();
        Factory.InsightsWorker.Unavailable = true;

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await Factory.FindTrackAsync(track.Id)).ShouldBeNull();
        Factory.Logs.Entries.ShouldContain(entry => entry.Message.Contains("quality job", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DeletingATrack_WithAFinishedReport_CallsNoWorker()
    {
        var track = await Factory.SeedAsync(SyncTestData.CompletedTrack());
        await new ApiTestFactoryPair(Factory, Client).MeasureAsync(track.Id, QualityTestData.SixGood("fake-1"));
        var before = Factory.InsightsWorker.Deleted.Count;

        using var deleted = await Client.DeleteAsync(new Uri(ApiRoutes.Track(track.Id), UriKind.Relative), Ct);

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.InsightsWorker.Deleted.Count.ShouldBe(before);
    }

    private static Exception Failure(string how) => how switch
    {
        "server-error" => new WorkerRequestException(HttpStatusCode.InternalServerError, "boom"),
        "refused" => new WorkerRequestException(HttpStatusCode.UnprocessableEntity, "no"),
        _ => new System.Text.Json.JsonException("unreadable"),
    };

    private async Task<(Guid First, Guid Second)> TwoRunningReportsAsync()
    {
        var first = await Factory.SeedAsync(SyncTestData.CompletedTrack("aaaaaaaaaaa"));
        var second = await Factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        (await Client.RequestQualityAsync(first.Id)).Dispose();
        (await Client.RequestQualityAsync(second.Id)).Dispose();
        return (first.Id, second.Id);
    }

    private string JobOf(int index) => Factory.InsightsWorker.QualityJobs.Keys.Order(StringComparer.Ordinal).ElementAt(index);

    private void CompleteAll()
    {
        foreach (var job in Factory.InsightsWorker.QualityJobs.Keys.ToList())
        {
            Factory.InsightsWorker.CompleteQuality(job);
        }
    }

    private Task<bool> DeleteRowAsync(Guid trackId) => Factory.WithScopeAsync(async sp =>
    {
        var tracks = sp.GetRequiredService<ITrackRepository>();
        await tracks.RemoveWithMixesAsync((await tracks.FindAsync(trackId, CancellationToken.None))!, CancellationToken.None);
        return true;
    });
}
