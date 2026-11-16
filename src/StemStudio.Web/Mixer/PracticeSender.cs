using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>
/// UI-side coalescing of practice changes (audio-engine.md "Live changes": the UI sends at most one change per 50 ms
/// while a slider is dragged). The first change after a quiet interval goes out at once, later ones within the interval
/// are replaced by the latest and sent when it ends. Settings equal to what the engine already has are never sent, so
/// a mixer that opens at the neutral defaults makes no <c>setPractice</c> call (SC-007). Audible timing stays in the
/// engine; this only limits interop traffic.
/// </summary>
/// <param name="time">The clock.</param>
/// <param name="send">Sends settings to the engine (marshals to the renderer itself; called from a timer thread too).</param>
public sealed class PracticeSender(TimeProvider time, Func<PracticeSettingsDto, Task> send) : IDisposable
{
    private readonly Lock _gate = new();
    private PracticeSettingsDto? _pending;
    private DateTimeOffset _lastSent = DateTimeOffset.MinValue;
    private ITimer? _timer;
    private bool _disposed;

    /// <summary>The minimum time between two engine calls.</summary>
    public static TimeSpan Interval { get; } = TimeSpan.FromMilliseconds(50);

    /// <summary>The settings last sent (what the engine has); the neutral defaults before the first call.</summary>
    public PracticeSettingsDto Sent { get; private set; } = PracticeSettingsDto.Default;

    /// <summary>Sends the settings now, or schedules them for the end of the current interval.</summary>
    /// <param name="settings">The new settings.</param>
    /// <returns>The engine call when sent at once, else a completed task.</returns>
    public Task SubmitAsync(PracticeSettingsDto settings)
    {
        lock (_gate)
        {
            var elapsed = time.GetUtcNow() - _lastSent;
            if (_timer is null && elapsed >= Interval)
            {
                return settings == Sent ? Task.CompletedTask : SendLocked(settings);
            }

            _pending = settings;
            _timer ??= time.CreateTimer(_ => Flush(), null, Interval - elapsed, Timeout.InfiniteTimeSpan);
            return Task.CompletedTask;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _pending = null;
            _timer?.Dispose();
            _timer = null;
        }
    }

    private void Flush()
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            var pending = _pending;
            _pending = null;
            if (!_disposed && pending is not null && pending != Sent)
            {
                _ = SendLocked(pending);
            }
        }
    }

    private Task SendLocked(PracticeSettingsDto settings)
    {
        Sent = settings;
        _lastSent = time.GetUtcNow();
        return send(settings);
    }
}
