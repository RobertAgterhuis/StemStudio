using StemStudio.Contracts.LibraryCare;
using StemStudio.Web.Api;

namespace StemStudio.Web.LibraryCare;

/// <summary>
/// The library care status of one circuit (013 T040): polls <c>GET /api/library-care/status</c> every
/// <see cref="BusyInterval"/> while a library job is queued or running or the library is switching, and every
/// <see cref="IdleInterval"/> otherwise. The shell's job indicator and maintenance overlay and the Library care page read
/// <see cref="Current"/> and re-render on <see cref="Changed"/>. A failed read keeps the last status.
/// </summary>
/// <param name="client">The library care client.</param>
/// <param name="time">The clock that paces the polls.</param>
public sealed class LibraryCareStatusSource(ILibraryCareClient client, TimeProvider time) : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private Task? _loop;
    private bool _disposed;

    /// <summary>Poll interval while a job runs or the gate is not open.</summary>
    public static TimeSpan BusyInterval { get; } = TimeSpan.FromSeconds(2);

    /// <summary>Poll interval while the library is idle.</summary>
    public static TimeSpan IdleInterval { get; } = TimeSpan.FromSeconds(10);

    /// <summary>Raised after every read that changed <see cref="Current"/>, and after <see cref="RefreshAsync"/>.</summary>
    public event Action? Changed;

    /// <summary>The last status read, or null before the first answer.</summary>
    public LibraryCareStatusDto? Current { get; private set; }

    /// <summary>The gate mode; <see cref="GateModeDto.Open"/> before the first answer.</summary>
    public GateModeDto Gate => Current?.Gate ?? GateModeDto.Open;

    /// <summary>The queued or running library job, or null.</summary>
    public LibraryJobDto? ActiveJob => Current?.ActiveJob;

    /// <summary>True while a job is active or the gate is not open: the polls then come every <see cref="BusyInterval"/>.</summary>
    public bool Busy => ActiveJob is not null || Gate != GateModeDto.Open;

    /// <summary>Starts the polling loop once (the first read happens at once).</summary>
    public void Start() => _loop ??= RunAsync(_stop.Token);

    /// <summary>Reads the status now (after the page started or cancelled a job) and raises <see cref="Changed"/>.</summary>
    /// <param name="ct">Cancels the request.</param>
    /// <returns>The status, or the last one when it could not be read.</returns>
    public async Task<LibraryCareStatusDto?> RefreshAsync(CancellationToken ct = default)
    {
        await ReadAsync(ct);
        Changed?.Invoke();
        return Current;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        await _stop.CancelAsync();
        if (_loop is not null)
        {
            await _loop;
        }

        _stop.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var before = Current;
                await ReadAsync(ct);
                if (!Equals(before, Current))
                {
                    Changed?.Invoke();
                }

                await Task.Delay(Busy ? BusyInterval : IdleInterval, time, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Circuit closed.
        }
    }

    private async Task ReadAsync(CancellationToken ct)
    {
        try
        {
            Current = await client.GetStatusAsync(ct);
        }
        catch (ApiException)
        {
            // Keep the last status; the next poll tries again.
        }
    }
}
