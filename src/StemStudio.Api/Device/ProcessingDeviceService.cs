using StemStudio.Api.Worker;
using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Device;

/// <summary>
/// Answers <c>GET /api/device</c> from the worker's <c>GET /health</c> (<c>deviceInfo</c>, worker contract 1.2.0). An
/// unreachable or failing worker becomes <see cref="ProcessingDeviceDto.Unreachable"/>; every answer is kept in the
/// <see cref="ProcessingDeviceCache"/> for <see cref="ProcessingDeviceCache.Lifetime"/>.
/// </summary>
/// <param name="worker">Worker client (bounded by <see cref="WorkerTimeouts.Health"/>).</param>
/// <param name="cache">Shared answer cache.</param>
public sealed class ProcessingDeviceService(IWorkerClient worker, ProcessingDeviceCache cache)
{
    /// <summary>The worker's active processing device.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>The device, or <see cref="ProcessingDeviceDto.Unreachable"/>.</returns>
    public async Task<ProcessingDeviceDto> GetAsync(CancellationToken cancellationToken)
    {
        if (cache.TryGet() is { } cached)
        {
            return cached;
        }

        var device = await AskWorkerAsync(cancellationToken);
        cache.Store(device);
        return device;
    }

    /// <summary>Maps the worker's device info to the contract DTO.</summary>
    /// <param name="info">The worker's device info.</param>
    /// <returns>A reachable device.</returns>
    public static ProcessingDeviceDto ToDto(WorkerDeviceInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        return new ProcessingDeviceDto
        {
            Reachable = true,
            Kind = Enum.Parse<ProcessingDeviceKindDto>(info.Kind.ToString()),
            Name = info.Name,
            TotalMemoryMiB = info.TotalMemoryMiB,
            Reason = info.Reason,
        };
    }

    private async Task<ProcessingDeviceDto> AskWorkerAsync(CancellationToken cancellationToken)
    {
        try
        {
            return ToDto((await worker.GetHealthAsync(cancellationToken)).DeviceInfo);
        }
        catch (Exception exception) when (exception is WorkerUnavailableException or WorkerRequestException)
        {
            return ProcessingDeviceDto.Unreachable;
        }
    }
}

/// <summary>Keeps the last <c>GET /api/device</c> answer for <see cref="Lifetime"/> (singleton, thread-safe).</summary>
/// <param name="clock">Clock.</param>
public sealed class ProcessingDeviceCache(TimeProvider clock)
{
    /// <summary>How long an answer is reused (backend contract 1.3.0: at most 10 seconds).</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromSeconds(10);

    private Entry? _entry;

    /// <summary>The cached answer while it is younger than <see cref="Lifetime"/>.</summary>
    /// <returns>The answer, or null.</returns>
    public ProcessingDeviceDto? TryGet()
    {
        var entry = Volatile.Read(ref _entry);
        return entry is not null && clock.GetUtcNow() - entry.StoredAt < Lifetime ? entry.Device : null;
    }

    /// <summary>Stores an answer.</summary>
    /// <param name="device">The answer.</param>
    public void Store(ProcessingDeviceDto device) => Volatile.Write(ref _entry, new Entry(device, clock.GetUtcNow()));

    private sealed record Entry(ProcessingDeviceDto Device, DateTimeOffset StoredAt);
}
