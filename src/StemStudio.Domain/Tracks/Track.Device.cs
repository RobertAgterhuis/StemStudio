namespace StemStudio.Domain.Tracks;

/// <summary>Processing device of the standard and the detail job (feature 003, data-model.md 3.1).</summary>
public sealed partial class Track
{
    /// <summary>Where the standard separation ran; null before it, for cached results and for older tracks.</summary>
    public JobDevice? Device { get; private set; }

    /// <summary>Copies the worker job's device while the track is not terminal; never changed after completion or failure.</summary>
    /// <param name="device">The reported device (null clears it, e.g. while queued or for a cached result).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyDevice(JobDevice? device, DateTime nowUtc)
    {
        if (IsTerminal || Equals(Device, device))
        {
            return;
        }

        (Device, UpdatedAt) = (device, nowUtc);
    }

    /// <summary>Copies the worker detail job's device into the queued or running detail phase; ignored otherwise.</summary>
    /// <param name="device">The reported device (null clears it).</param>
    /// <param name="nowUtc">Current time (UTC).</param>
    public void ApplyDetailDevice(JobDevice? device, DateTime nowUtc)
    {
        if (!IsDetailActive || Equals(DetailPhase!.Device, device))
        {
            return;
        }

        SetDetail(DetailPhase with { Device = device, UpdatedAt = nowUtc }, nowUtc);
    }
}
