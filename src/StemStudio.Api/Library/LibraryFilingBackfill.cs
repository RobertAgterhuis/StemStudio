using StemStudio.Api.LibraryCare.Gate;
using StemStudio.Domain.Library;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Library;

/// <summary>Starts the background filing when a library read sees unfiled items (feature 022, research R-11 refinement 1).</summary>
public interface ILibraryFilingTrigger
{
    /// <summary>Items the last run counted as not filed yet.</summary>
    int Pending { get; }

    /// <summary>Asks for a run; a no-op while a run is already requested (single-flight).</summary>
    void Trigger();
}

/// <summary>
/// Files every item whose <c>FiledVersion</c> is older than <see cref="LibraryFilingRules.Version"/> (FR-010, SC-003): at
/// start (after <see cref="StartDelay"/>) and whenever <see cref="ILibraryFilingTrigger.Trigger"/> is called (a 013 restore switches the library without
/// a restart), in batches of <c>StemStudio:LibraryBackfillBatchSize</c> (default 500), one transaction per batch. Waits
/// while the 013 gate is in maintenance before every batch and re-counts the pending items after such a wait.
/// </summary>
/// <param name="scopes">Scope factory (one scope per batch).</param>
/// <param name="gate">The 013 library gate.</param>
/// <param name="configuration">Configuration (batch size).</param>
/// <param name="clock">Clock of the start-up delay.</param>
/// <param name="logger">Logger.</param>
public sealed partial class LibraryFilingBackfill(
    IServiceScopeFactory scopes, LibraryGate gate, IConfiguration configuration, TimeProvider clock, ILogger<LibraryFilingBackfill> logger)
    : BackgroundService, ILibraryFilingTrigger
{
    /// <summary>Configuration key of the batch size.</summary>
    public const string BatchSizeKey = "StemStudio:LibraryBackfillBatchSize";

    /// <summary>Default batch size.</summary>
    public const int DefaultBatchSize = 500;

    /// <summary>Delay of the start-up run after the host started (keeps it out of the start-up work of other services).</summary>
    public static readonly TimeSpan StartDelay = TimeSpan.FromSeconds(2);

    private readonly SemaphoreSlim _wake = new(0, 1);
    private readonly SemaphoreSlim _running = new(1, 1);
    private readonly Lock _lock = new();
    private int _pending;

    /// <inheritdoc />
    public int Pending => Volatile.Read(ref _pending);

    private int BatchSize => Math.Max(1, configuration.GetValue(BatchSizeKey, DefaultBatchSize));

    /// <inheritdoc />
    public void Trigger()
    {
        lock (_lock)
        {
            if (_wake.CurrentCount == 0)
            {
                _wake.Release();
            }
        }
    }

    /// <summary>Files batches until no item is pending (serialised with any other run).</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The number of items filed.</returns>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        await _running.WaitAsync(cancellationToken);
        try
        {
            return await RunBatchesAsync(cancellationToken);
        }
        finally
        {
            _running.Release();
        }
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        _wake.Dispose();
        _running.Dispose();
        base.Dispose();
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _ = TriggerAfterStartDelayAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            await _wake.WaitAsync(stoppingToken);
            await RunSafelyAsync(stoppingToken);
        }
    }

    private async Task TriggerAfterStartDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartDelay, clock, stoppingToken);
            Trigger();
        }
        catch (OperationCanceledException)
        {
            // The host stopped before the start-up run.
        }
    }

    private async Task RunSafelyAsync(CancellationToken cancellationToken)
    {
        try
        {
            var filed = await RunOnceAsync(cancellationToken);
            LogFiled(logger, filed);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogRunFailed(logger, exception);
        }
    }

    private async Task<int> RunBatchesAsync(CancellationToken cancellationToken)
    {
        var total = 0;
        int filed;
        do
        {
            var waited = gate.IsMaintenance;
            await gate.WaitWhileMaintenanceAsync(cancellationToken);
            filed = await FileBatchAsync(waited, cancellationToken);
            total += filed;
        }
        while (filed > 0);

        return total;
    }

    private async Task<int> FileBatchAsync(bool recount, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var library = scope.ServiceProvider.GetRequiredService<ILibraryRepository>();
        if (recount)
        {
            Volatile.Write(ref _pending, await library.CountPendingAsync(cancellationToken));
        }

        var filed = await FileAsync(library, scope.ServiceProvider.GetRequiredService<ILibraryFiler>(), cancellationToken);
        Volatile.Write(ref _pending, await library.CountPendingAsync(cancellationToken));
        return filed;
    }

    private async Task<int> FileAsync(ILibraryRepository library, ILibraryFiler filer, CancellationToken cancellationToken)
    {
        var filed = await filer.FileAllAsync(await library.PendingAsync(BatchSize, cancellationToken), cancellationToken);
        if (filed > 0)
        {
            await library.SaveAsync(cancellationToken);
        }

        return filed;
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Library filing: {Filed} items filed")]
    private static partial void LogFiled(ILogger logger, int filed);

    [LoggerMessage(Level = LogLevel.Error, Message = "Library filing failed; it runs again on the next library read")]
    private static partial void LogRunFailed(ILogger logger, Exception exception);
}
