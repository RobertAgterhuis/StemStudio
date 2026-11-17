using Microsoft.Extensions.Options;
using StemStudio.Api.Assets;
using StemStudio.Api.Detail;
using StemStudio.Api.Lyrics;
using StemStudio.Api.Metadata;
using StemStudio.Api.ScoreFollow;
using StemStudio.Api.SongSections;

namespace StemStudio.Api.Sync;

/// <summary>
/// Background poller (ADR-003): every <see cref="StemStudioApiOptions.JobPollIntervalMs"/> on the injected
/// <see cref="TimeProvider"/> it fails orphaned imports (<see cref="OrphanedImportSweeper"/>), runs one
/// <see cref="ITrackSynchronizer"/> pass, one <see cref="DetailSynchronizer"/> pass and one <see cref="MetadataSynchronizer"/> pass
/// (feature 005), one <see cref="LyricsSynchronizer"/> pass (feature 006) and one <see cref="AssetSynchronizer"/> pass (feature 007),
/// in a new scope.
/// </summary>
/// <param name="scopes">Scope factory.</param>
/// <param name="options">API options.</param>
/// <param name="clock">Clock driving the timer (FakeTimeProvider in tests).</param>
/// <param name="logger">Logger.</param>
public sealed class JobSyncService(
    IServiceScopeFactory scopes, IOptions<StemStudioApiOptions> options, TimeProvider clock, ILogger<JobSyncService> logger) : PeriodicSyncService(scopes, clock, logger)
{
    /// <inheritdoc />
    protected override TimeSpan Interval => TimeSpan.FromMilliseconds(Math.Max(options.Value.JobPollIntervalMs, 1));

    /// <inheritdoc />
    protected override async Task PassAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        await services.GetRequiredService<OrphanedImportSweeper>().SweepAsync(cancellationToken);
        await services.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<DetailSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<MetadataSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<LyricsSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<AssetSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<SectionJobApplier>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<ScoreViewJobApplier>().SyncAllAsync(cancellationToken);
    }
}
