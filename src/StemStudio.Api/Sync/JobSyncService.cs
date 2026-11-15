using Microsoft.Extensions.Options;
using StemStudio.Api.Assets;
using StemStudio.Api.Detail;
using StemStudio.Api.Lyrics;
using StemStudio.Api.Metadata;

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
public sealed partial class JobSyncService(
    IServiceScopeFactory scopes, IOptions<StemStudioApiOptions> options, TimeProvider clock, ILogger<JobSyncService> logger) : BackgroundService
{
    private PeriodicTimer? _timer;

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Created before ExecuteAsync runs (it may start on another thread), so no tick of the clock is missed.
        var interval = TimeSpan.FromMilliseconds(Math.Max(options.Value.JobPollIntervalMs, 1));
        _timer = new PeriodicTimer(interval, clock);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (await _timer!.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await PassesAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogTickFailed(logger, exception);
        }
    }

    private static async Task PassesAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        await services.GetRequiredService<OrphanedImportSweeper>().SweepAsync(cancellationToken);
        await services.GetRequiredService<ITrackSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<DetailSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<MetadataSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<LyricsSynchronizer>().SyncAllAsync(cancellationToken);
        await services.GetRequiredService<AssetSynchronizer>().SyncAllAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Job synchronisation tick failed; retrying on the next tick")]
    private static partial void LogTickFailed(ILogger logger, Exception exception);
}
