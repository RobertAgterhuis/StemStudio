using Microsoft.Extensions.DependencyInjection;
using StemStudio.Api.Sync;
using StemStudio.Api.Tests.TestSupport;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Tests.Sync;

/// <summary>A queued track whose worker job was never created (client gone between insert and worker call) is failed.</summary>
public sealed class OrphanedImportSweeperTests : IDisposable
{
    private readonly ApiFactory _factory = new();

    public OrphanedImportSweeperTests() => _factory.CreateClient().Dispose();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task QueuedTrackWithoutJob_OlderThanTheTimeout_FailsRetryably()
    {
        var orphan = await _factory.SeedAsync(SyncTestData.UrlTrack(jobId: null));
        _factory.Clock.Advance(TimeSpan.FromSeconds(121));

        await SweepAsync();

        var swept = (await _factory.FindTrackAsync(orphan.Id))!;
        swept.Status.ShouldBe(TrackStatus.Failed);
        swept.Error.ShouldBe(OrphanedImportSweeper.ImportInterrupted);
        swept.CanRetry.ShouldBeTrue();
        swept.UpdatedAt.ShouldBe(_factory.Clock.GetUtcNow().UtcDateTime);
    }

    [Fact]
    public async Task QueuedTrackWithoutJob_YoungerThanTheTimeout_IsKept()
    {
        var pending = await _factory.SeedAsync(SyncTestData.UrlTrack(jobId: null));
        _factory.Clock.Advance(TimeSpan.FromSeconds(120));

        await SweepAsync();

        (await _factory.FindTrackAsync(pending.Id))!.Status.ShouldBe(TrackStatus.Queued);
    }

    [Fact]
    public async Task TracksWithJobOrTerminal_AreNeverSwept()
    {
        var running = await _factory.SeedAsync(SyncTestData.UrlTrack("aaaaaaaaaaa"));
        var completed = await _factory.SeedAsync(SyncTestData.CompletedTrack("bbbbbbbbbbb"));
        var failed = await _factory.SeedAsync(SyncTestData.FailedTrack("ccccccccccc"));
        _factory.Clock.Advance(TimeSpan.FromHours(1));

        await SweepAsync();

        (await _factory.FindTrackAsync(running.Id))!.Status.ShouldBe(TrackStatus.Queued);
        (await _factory.FindTrackAsync(completed.Id))!.Status.ShouldBe(TrackStatus.Completed);
        (await _factory.FindTrackAsync(failed.Id))!.Error.ShouldBe("interrupted by restart");
    }

    [Fact]
    public async Task HostedService_SweepsOrphansOnEachTick()
    {
        var orphan = await _factory.SeedAsync(SyncTestData.UrlTrack(jobId: null));

        _factory.Clock.Advance(TimeSpan.FromSeconds(121));

        await Eventually.WaitUntilAsync(async () => (await _factory.FindTrackAsync(orphan.Id))!.Status == TrackStatus.Failed);
    }

    private Task<bool> SweepAsync() => _factory.WithScopeAsync(async sp =>
    {
        await sp.GetRequiredService<OrphanedImportSweeper>().SweepAsync(TestContext.Current.CancellationToken);
        return true;
    });
}
