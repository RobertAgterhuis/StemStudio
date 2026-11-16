namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// GPU-heavy job submissions in flight (feature 011, B2/SC-008): the <see cref="GpuJobGate"/> opens a hold before it releases the
/// local model and the decorator closes it when the submission returns; <see cref="GpuActivity"/> reports the GPU as busy while a
/// hold is open and for <see cref="Grace"/> after the last one closed, so no agent turn picks the primary model between the
/// release and the moment the caller has stored the job's queued state.
/// </summary>
/// <param name="clock">Clock of the grace period.</param>
public sealed class GpuSubmissions(TimeProvider clock)
{
    /// <summary>How long the GPU stays busy after a submission returned (the caller stores the job right after it).</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(10);

    private readonly Lock _gate = new();
    private int _open;
    private DateTimeOffset _graceUntil = DateTimeOffset.MinValue;

    /// <summary>True while a hold is open or closed less than <see cref="Grace"/> ago.</summary>
    public bool Holding
    {
        get
        {
            lock (_gate)
            {
                return _open > 0 || clock.GetUtcNow() < _graceUntil;
            }
        }
    }

    /// <summary>Opens a hold.</summary>
    /// <returns>The hold; disposing it closes the hold (once) and starts the grace period.</returns>
    public IDisposable Begin()
    {
        lock (_gate)
        {
            _open++;
        }

        return new Hold(this);
    }

    private void Close()
    {
        lock (_gate)
        {
            _open--;
            _graceUntil = clock.GetUtcNow() + Grace;
        }
    }

    private sealed class Hold(GpuSubmissions owner) : IDisposable
    {
        private int _closed;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _closed, 1) == 0)
            {
                owner.Close();
            }
        }
    }
}
