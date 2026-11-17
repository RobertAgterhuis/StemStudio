namespace StemStudio.Api.LibraryCare.Gate;

/// <summary>Mode of the library write gate (data-model 5.2).</summary>
public enum LibraryGateMode
{
    /// <summary>Everything is allowed.</summary>
    Open,

    /// <summary>A backup runs: destructive operations answer <c>409 backup-running</c>.</summary>
    BackupRunning,

    /// <summary>A restore switches the library: every <c>/api/*</c> route but three answers <c>503 library-maintenance</c>.</summary>
    Maintenance,
}

/// <summary>
/// The library write gate (FR-002, FR-013): one process-wide mode, the in-flight request counter the restore switch waits
/// for, and a signal for background work that pauses during maintenance.
/// </summary>
/// <param name="clock">Clock of the idle timeout.</param>
public sealed class LibraryGate(TimeProvider clock)
{
    private readonly Lock _lock = new();
    private TaskCompletionSource _idle = Completed();
    private TaskCompletionSource _open = Completed();
    private int _inFlight;

    /// <summary>The current mode.</summary>
    public LibraryGateMode Mode { get; private set; }

    /// <summary>True while a restore switches the library.</summary>
    public bool IsMaintenance => Mode == LibraryGateMode.Maintenance;

    /// <summary>Requests being served that maintenance waits for.</summary>
    public int InFlight
    {
        get
        {
            lock (_lock)
            {
                return _inFlight;
            }
        }
    }

    /// <summary>Closes the gate for destructive operations when it is open.</summary>
    /// <returns>True when the backup mode was entered.</returns>
    public bool TryBeginBackup()
    {
        lock (_lock)
        {
            if (Mode != LibraryGateMode.Open)
            {
                return false;
            }

            Mode = LibraryGateMode.BackupRunning;
            return true;
        }
    }

    /// <summary>Opens the gate after a backup (no effect in another mode).</summary>
    public void EndBackup()
    {
        lock (_lock)
        {
            Mode = Mode == LibraryGateMode.BackupRunning ? LibraryGateMode.Open : Mode;
        }
    }

    /// <summary>Enters maintenance: background work pauses and requests are refused.</summary>
    public void EnterMaintenance()
    {
        lock (_lock)
        {
            Mode = LibraryGateMode.Maintenance;
            _open = _open.Task.IsCompleted ? Pending() : _open;
        }
    }

    /// <summary>Leaves maintenance and opens the gate.</summary>
    public void ExitMaintenance()
    {
        lock (_lock)
        {
            Mode = LibraryGateMode.Open;
            _open.TrySetResult();
        }
    }

    /// <summary>Counts one request until the returned handle is disposed (disposing twice counts once).</summary>
    /// <returns>The handle.</returns>
    public IDisposable TrackRequest()
    {
        lock (_lock)
        {
            _inFlight++;
            _idle = _idle.Task.IsCompleted ? Pending() : _idle;
        }

        return new RequestHandle(this);
    }

    /// <summary>Waits until no counted request is in flight.</summary>
    /// <param name="timeout">Longest wait (a streaming request may never end).</param>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>True when idle, false after the timeout.</returns>
    public async Task<bool> WaitForIdleAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        Task idle;
        lock (_lock)
        {
            idle = _inFlight == 0 ? Task.CompletedTask : _idle.Task;
        }

        try
        {
            await idle.WaitAsync(timeout, clock, cancellationToken);
            return true;
        }
        catch (TimeoutException)
        {
            return false;
        }
    }

    /// <summary>Returns at once when not in maintenance, otherwise when maintenance ends.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task.</returns>
    public Task WaitWhileMaintenanceAsync(CancellationToken cancellationToken)
    {
        lock (_lock)
        {
            return IsMaintenance ? _open.Task.WaitAsync(cancellationToken) : Task.CompletedTask;
        }
    }

    private static TaskCompletionSource Pending() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static TaskCompletionSource Completed()
    {
        var source = Pending();
        source.SetResult();
        return source;
    }

    private void Release()
    {
        lock (_lock)
        {
            _inFlight--;
            if (_inFlight == 0)
            {
                _idle.TrySetResult();
            }
        }
    }

    private sealed class RequestHandle(LibraryGate gate) : IDisposable
    {
        private int _disposed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
