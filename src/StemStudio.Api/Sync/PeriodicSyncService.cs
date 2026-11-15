namespace StemStudio.Api.Sync;

/// <summary>
/// A background loop that runs one synchronisation pass per tick of a <see cref="PeriodicTimer"/> on the injected
/// clock, in a fresh scope; a failing pass is logged and retried on the next tick.
/// </summary>
/// <param name="scopes">Scope factory.</param>
/// <param name="clock">Clock of the timer.</param>
/// <param name="logger">Logger.</param>
public abstract partial class PeriodicSyncService(IServiceScopeFactory scopes, TimeProvider clock, ILogger logger) : BackgroundService
{
    private PeriodicTimer? _timer;

    /// <summary>Time between passes.</summary>
    protected abstract TimeSpan Interval { get; }

    /// <inheritdoc />
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Created before ExecuteAsync runs (it may start on another thread), so no tick of the clock is missed.
        _timer = new PeriodicTimer(Interval, clock);
        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>One pass.</summary>
    /// <param name="services">The scope's services.</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    protected abstract Task PassAsync(IServiceProvider services, CancellationToken cancellationToken);

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
            await PassAsync(scope.ServiceProvider, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogTickFailed(logger, GetType().Name, exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "{Service} tick failed; retrying on the next tick")]
    private static partial void LogTickFailed(ILogger logger, string service, Exception exception);
}
