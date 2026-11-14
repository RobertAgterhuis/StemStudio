namespace StemStudio.Web.Services;

/// <summary>Paces status polling; abstracted so tests can tick it deterministically.</summary>
public interface IPollingTimer
{
    /// <summary>Waits for the next poll.</summary>
    /// <param name="cancellationToken">Stops waiting.</param>
    /// <returns>True when it is time to poll; false when cancelled.</returns>
    Task<bool> WaitAsync(CancellationToken cancellationToken);
}

/// <summary>Waits <see cref="Interval"/> (about 1 s) on a <see cref="TimeProvider"/> between polls.</summary>
/// <param name="time">The clock.</param>
public sealed class PollingTimer(TimeProvider time) : IPollingTimer
{
    /// <summary>Polling interval (plan: progress polled about once per second).</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public async Task<bool> WaitAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(Interval, time, cancellationToken);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
