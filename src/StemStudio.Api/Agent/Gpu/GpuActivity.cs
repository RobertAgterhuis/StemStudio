using StemStudio.Agent.Ports;
using StemStudio.Api.Device;
using StemStudio.Api.Generator;
using StemStudio.Api.Worker;
using StemStudio.Contracts.Tracks;
using StemStudio.Domain.Assets;
using StemStudio.Domain.Generations;
using StemStudio.Domain.Lyrics;
using StemStudio.Domain.Tracks;
using StemStudio.Persistence.Repositories;

namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// <see cref="IGpuActivity"/> from the API's own job states (FR-021): true while a generator or singer job is active, or while a
/// track's standard or detail job, a lyrics alignment or a score transcription is queued or running on the GPU (or on an unknown
/// device unless the worker is known to be CPU-only). The answer is cached for 2 s.
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
        var sp = scope.ServiceProvider;
        var cpuWorker = devices.TryGet()?.Kind == ProcessingDeviceKindDto.Cpu;
        return Wait(sp.GetRequiredService<IGenerationRepository>().ListActiveAsync(CancellationToken.None)).Any(HoldsGpu)
            || (!cpuWorker && HasWorkerJobWithoutDevice(sp))
            || Wait(sp.GetRequiredService<ITrackRepository>().ListAsync(CancellationToken.None)).Any(track => HoldsGpu(track, cpuWorker));
    }

    // Lyrics alignment and score transcription report no device of their own: they count unless the worker is CPU-only.
    private static bool HasWorkerJobWithoutDevice(IServiceProvider sp) =>
        Wait(sp.GetRequiredService<ITrackRepository>().ListLyricsActiveAsync(CancellationToken.None)).Any(track => track.Lyrics!.Status == LyricsStatus.Aligning)
        || Wait(sp.GetRequiredService<IAssetRepository>().ListActiveAsync(CancellationToken.None)).Any(asset => asset.Kind == AssetKind.Score);

    // The generator and the singer run on the GPU only; a conversion waiting for its take's separation is counted by the track.
    private static bool HoldsGpu(Generation generation) => generation.Status != GenerationState.WaitingForStems;

    private static IReadOnlyList<T> Wait<T>(Task<IReadOnlyList<T>> query) => query.GetAwaiter().GetResult();

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
        services.AddSingleton<GpuSubmissions>();
        services.AddSingleton<GpuJobGate>();
        services.AddSingleton<IGpuActivity, GpuActivity>();
        return services;
    }

    /// <summary>
    /// Wraps the registered <see cref="IWorkerClient"/>, <see cref="IGeneratorClient"/> and <see cref="ISingerClient"/> in their
    /// GPU-releasing decorators. Call after the clients (and any replacement of them) are registered.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection DecorateGpuJobClients(this IServiceCollection services) => services
        .Decorate<IWorkerClient>((sp, inner) => new GpuReleasingWorkerClient(inner, sp.GetRequiredService<GpuJobGate>()))
        .Decorate<IGeneratorClient>((sp, inner) => new GpuReleasingGeneratorClient(inner, sp.GetRequiredService<GpuJobGate>()))
        .Decorate<ISingerClient>((sp, inner) => new GpuReleasingSingerClient(inner, sp.GetRequiredService<GpuJobGate>()));

    private static IServiceCollection Decorate<T>(this IServiceCollection services, Func<IServiceProvider, T, T> decorate)
        where T : class
    {
        if (services.LastOrDefault(descriptor => descriptor.ServiceType == typeof(T)) is not { } inner)
        {
            return services;
        }

        services.Remove(inner);
        services.Add(new ServiceDescriptor(typeof(T), sp => decorate(sp, Resolve<T>(sp, inner)), inner.Lifetime));
        return services;
    }

    private static T Resolve<T>(IServiceProvider services, ServiceDescriptor descriptor) => (T)(descriptor.ImplementationInstance
        ?? descriptor.ImplementationFactory?.Invoke(services)
        ?? ActivatorUtilities.CreateInstance(services, descriptor.ImplementationType!));
}
