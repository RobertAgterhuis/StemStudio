using StemStudio.Api.Worker;
using StemStudio.Domain;
using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Sync;

/// <summary>
/// Copies the device of a worker job or detail job to a track (feature 003, data-model.md 3.1). A device that breaks
/// the domain rules is logged and ignored: the track keeps its device and is never failed for it.
/// </summary>
/// <param name="clock">Clock.</param>
/// <param name="logger">Logger.</param>
public sealed partial class JobDeviceApplier(TimeProvider clock, ILogger<JobDeviceApplier> logger)
{
    /// <summary>Applies a standard job's device to the track (not saved).</summary>
    /// <param name="track">The track.</param>
    /// <param name="device">The worker's device, or null.</param>
    public void ApplyStandard(Track track, WorkerJobDevice? device)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (TryConvert(track.Id, device, out var converted))
        {
            track.ApplyDevice(converted, clock.GetUtcNow().UtcDateTime);
        }
    }

    /// <summary>Applies a detail job's device to the track's active detail phase (not saved).</summary>
    /// <param name="track">The track.</param>
    /// <param name="device">The worker's device, or null.</param>
    public void ApplyDetail(Track track, WorkerJobDevice? device)
    {
        ArgumentNullException.ThrowIfNull(track);
        if (TryConvert(track.Id, device, out var converted))
        {
            track.ApplyDetailDevice(converted, clock.GetUtcNow().UtcDateTime);
        }
    }

    private bool TryConvert(Guid trackId, WorkerJobDevice? device, out JobDevice? converted)
    {
        converted = null;
        try
        {
            converted = device is null ? null : new JobDevice(device.Kind, device.Name, device.CpuFallback, device.FallbackReason);
            return true;
        }
        catch (DomainValidationException invalid)
        {
            LogInvalidDevice(logger, trackId, invalid.Message);
            return false;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Track {TrackId}: ignoring an invalid device from the processing service: {Reason}")]
    private static partial void LogInvalidDevice(ILogger logger, Guid trackId, string reason);
}
