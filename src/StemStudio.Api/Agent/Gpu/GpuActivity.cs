using StemStudio.Agent.Ports;
using StemStudio.Api.Device;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// <see cref="IGpuActivity"/> from the API's own job states (FR-021): true while a track's standard or detail job is queued or
/// running on the GPU, or on an unknown device unless the worker is known to be CPU-only. The answer is cached for 2 s.
/// </summary>
/// <param name="scopes">Scope per evaluation.</param>
/// <param name="devices">Cached worker device.</param>
/// <param name="clock">Clock of the cache.</param>
public sealed class GpuActivity(IServiceScopeFactory scopes, ProcessingDeviceCache devices, TimeProvider clock) : IGpuActivity
{
    private static readonly TimeSpan CacheLifetime = TimeSpan.FromSeconds(2);
    private (bool Active, DateTimeOffset At)? _cached;

    /// <inheritdoc />
    public bool IsHeavyJobActive()
    {
        var now = clock.GetUtcNow();
        if (_cached is { } cached && now - cached.At < CacheLifetime)
        {
            return cached.Active;
        }

        var active = Evaluate();
        _cached = (active, now);
        return active;
    }

    private bool Evaluate()
    {
        using var scope = scopes.CreateScope();
        var tracks = scope.ServiceProvider.GetRequiredService<ITrackRepository>().ListAsync(CancellationToken.None).GetAwaiter().GetResult();
        var cpuWorker = devices.TryGet()?.Kind == ProcessingDeviceKindDto.Cpu;
        return tracks.Any(track => HoldsGpu(track, cpuWorker));
    }

    private static bool HoldsGpu(Track track, bool cpuWorker) =>
        (!track.IsTerminal && OnGpu(track.Device, cpuWorker)) || (track.IsDetailActive && OnGpu(track.DetailPhase!.Device, cpuWorker));

    private static bool OnGpu(JobDevice? device, bool cpuWorker) => device is null ? !cpuWorker : device.Kind == ProcessingDeviceKind.Gpu;
}

/// <summary>Registers the GPU coordination of feature 011.</summary>
public static class GpuRegistration
{
    /// <summary>Adds the gate and the GPU activity.</summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddGpuCoordination(this IServiceCollection services)
    {
        services.AddSingleton<GpuJobGate>();
        services.AddSingleton<IGpuActivity, GpuActivity>();
        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="IWorkerClient"/> in <see cref="GpuReleasingWorkerClient"/>. Call after the worker client
    /// (and any replacement of it) is registered.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection DecorateWorkerClient(this IServiceCollection services)
    {
        var inner = services.Last(descriptor => descriptor.ServiceType == typeof(IWorkerClient));
        services.Remove(inner);
        services.Add(new ServiceDescriptor(typeof(IWorkerClient), sp => new GpuReleasingWorkerClient(Resolve(sp, inner), sp.GetRequiredService<GpuJobGate>()), inner.Lifetime));
        return services;
    }

    private static IWorkerClient Resolve(IServiceProvider services, ServiceDescriptor descriptor) => (IWorkerClient)(descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(services)
        ?? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType!));
}
