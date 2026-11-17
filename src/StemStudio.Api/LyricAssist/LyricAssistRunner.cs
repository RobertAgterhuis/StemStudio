using StemStudio.Domain.Lyrics.Assist;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.LyricAssist;

/// <summary>
/// Runs at most one assist at a time in the background (data-model 3: one non-terminal assist globally). Each run gets its own scope;
/// an unexpected error fails the assist with <c>internal</c> (no exception text leaves the server).
/// </summary>
/// <param name="scopes">Scope factory.</param>
/// <param name="logger">Logger (never logs lyric text).</param>
public sealed class LyricAssistRunner(IServiceScopeFactory scopes, ILogger<LyricAssistRunner> logger) : IDisposable
{
    /// <summary>Message of an unexpected failure.</summary>
    public const string InternalMessage = "The assistant stopped because of an internal error.";

    private readonly Lock _lock = new();
    private readonly SemaphoreSlim _admission = new(1, 1);
    private (Guid Id, CancellationTokenSource Cancel, Task Run)? _current;

    /// <summary>The id of the running assist, if any.</summary>
    public Guid? RunningId
    {
        get
        {
            lock (_lock)
            {
                return _current is { Run.IsCompleted: false } current ? current.Id : null;
            }
        }
    }

    /// <summary>
    /// Serialises "is an assist active? -> store -> start" across requests (one assist globally, also for concurrent editor and agent
    /// requests). Dispose the result to release.
    /// </summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The admission; dispose it.</returns>
    public async Task<IDisposable> AdmitAsync(CancellationToken cancellationToken)
    {
        await _admission.WaitAsync(cancellationToken);
        return new Admission(_admission);
    }

    /// <summary>Starts an assist in the background.</summary>
    /// <param name="assistId">The queued assist.</param>
    /// <exception cref="InvalidOperationException">Another assist is still running (callers hold <see cref="AdmitAsync"/>).</exception>
    public void Start(Guid assistId)
    {
        lock (_lock)
        {
            if (_current is { Run.IsCompleted: false })
            {
                throw new InvalidOperationException("Another lyric assist is still running.");
            }

            _current?.Cancel.Dispose();
            var cancel = new CancellationTokenSource();
            _current = (assistId, cancel, Task.Run(() => RunAsync(assistId, cancel.Token)));
        }
    }

    /// <summary>Cancels the assist when it runs here and waits until it ended.</summary>
    /// <param name="assistId">The assist.</param>
    /// <returns>True when it was running here.</returns>
    public async Task<bool> CancelAsync(Guid assistId)
    {
        Task run;
        lock (_lock)
        {
            if (_current is not { } current || current.Id != assistId || current.Run.IsCompleted)
            {
                return false;
            }

            current.Cancel.Cancel();
            run = current.Run;
        }

        await run;
        return true;
    }

    /// <summary>Completes when no assist runs (tests and shutdown).</summary>
    /// <returns>A task.</returns>
    public Task WhenIdleAsync()
    {
        lock (_lock)
        {
            return _current?.Run ?? Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_lock)
        {
            _current?.Cancel.Cancel();
            _current?.Cancel.Dispose();
        }

        _admission.Dispose();
    }

    private sealed class Admission(SemaphoreSlim gate) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }

    private async Task RunAsync(Guid assistId, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<LyricAssistExecutor>().ExecuteAsync(assistId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await CancelledAsync(assistId);
        }
        catch (Exception exception)
        {
            logger.LogError("Lyric assist {AssistId} failed: {ErrorType}", assistId, exception.GetType().Name);
            await FailAsync(assistId);
        }
    }

    // A cancel that arrives before the executor took the assist over (first read) still ends it, so no queued assist is left behind.
    private async Task CancelledAsync(Guid assistId)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<ILyricAssistRepository>().FindAsync(assistId, CancellationToken.None) is { } assist && !assist.Status.IsTerminal())
        {
            await scope.ServiceProvider.GetRequiredService<LyricAssistExecutor>().CancelledAsync(assist);
        }
    }

    private async Task FailAsync(Guid assistId)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (await scope.ServiceProvider.GetRequiredService<ILyricAssistRepository>().FindAsync(assistId, CancellationToken.None) is { } assist && !assist.Status.IsTerminal())
        {
            await scope.ServiceProvider.GetRequiredService<LyricAssistExecutor>().FailAsync(assist, AssistFailureCode.Internal, InternalMessage, null);
        }
    }
}

/// <summary>Fails the assists that were queued or running when the API stopped (data-model 3: <c>interrupted</c>).</summary>
/// <param name="scopes">Scope factory.</param>
public sealed class LyricAssistStartupRecovery(IServiceScopeFactory scopes) : IHostedService
{
    /// <summary>Message of an interrupted assist.</summary>
    public const string InterruptedMessage = "The assist was interrupted because StemStudio restarted. Run it again.";

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var executor = scope.ServiceProvider.GetRequiredService<LyricAssistExecutor>();
        foreach (var assist in await scope.ServiceProvider.GetRequiredService<ILyricAssistRepository>().ActiveAsync(cancellationToken))
        {
            await executor.FailAsync(assist, AssistFailureCode.Interrupted, InterruptedMessage, null);
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
