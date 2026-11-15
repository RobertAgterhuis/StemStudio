namespace StemStudio.Web.Services;

/// <summary>
/// One polling loop of a page or panel: while <c>needed</c> is true, waits for the <see cref="IPollingTimer"/> and runs
/// <c>tick</c>; at most one loop runs at a time. Shared by the track page, the band info panel and the library.
/// </summary>
/// <param name="timer">The polling timer.</param>
public sealed class PollLoop(IPollingTimer timer)
{
    private Task? _running;

    /// <summary>Starts the loop unless it is already running or not needed.</summary>
    /// <param name="needed">Whether another poll is needed (checked before every wait).</param>
    /// <param name="tick">One poll (load and re-render).</param>
    /// <param name="settled">Action after the loop ended because nothing is needed any more.</param>
    /// <param name="ct">Stops the loop (component disposed).</param>
    public void Start(Func<bool> needed, Func<CancellationToken, Task> tick, Func<Task> settled, CancellationToken ct)
    {
        if (needed() && _running is not { IsCompleted: false })
        {
            _running = RunAsync(needed, tick, settled, ct);
        }
    }

    private async Task RunAsync(Func<bool> needed, Func<CancellationToken, Task> tick, Func<Task> settled, CancellationToken ct)
    {
        while (needed() && await timer.WaitAsync(ct))
        {
            await tick(ct);
        }

        if (!ct.IsCancellationRequested)
        {
            await settled();
        }
    }
}
