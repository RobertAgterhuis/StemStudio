using StemStudio.Agent.Ports;
using StemStudio.Api.Device;
using StemStudio.Contracts.Agent;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Api.Agent.Gpu;

/// <summary>
/// Frees the GPU memory of the local model before a GPU-heavy job is submitted (contracts/DELTA.md section 5, FR-022, SC-008):
/// calls the agent library's <see cref="ILlmGpuRelease"/> when the selected backend is Ollama and the job will use the GPU (a worker
/// job unless the worker is known to run on the CPU; every generator and singer job). Best effort: waits at most 2 s and never
/// fails the request.
/// </summary>
/// <param name="services">Resolves the optional <see cref="ILlmGpuRelease"/>.</param>
/// <param name="settings">Agent settings (selected backend).</param>
/// <param name="devices">Cached worker device (<c>GET /api/device</c>).</param>
/// <param name="clock">Clock of the 2 s limit.</param>
public sealed class GpuJobGate(IServiceProvider services, IAgentSettingsStore settings, ProcessingDeviceCache devices, TimeProvider clock)
{
    /// <summary>Longest wait for the release.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    /// <summary>Releases the local model's GPU memory before a worker job, unless the worker is known to run on the CPU.</summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>A task that never faults because of the release.</returns>
    public Task BeforeGpuJobAsync(CancellationToken cancellationToken) =>
        devices.TryGet()?.Kind == ProcessingDeviceKindDto.Cpu ? Task.CompletedTask : ReleaseAsync(cancellationToken);

    /// <summary>
    /// Releases the local model's GPU memory before a generator or singer job. These services run on the GPU only, so the
    /// worker device does not matter.
    /// </summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>A task that never faults because of the release.</returns>
    public Task BeforeGeneratorJobAsync(CancellationToken cancellationToken) => ReleaseAsync(cancellationToken);

    private async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        if (services.GetService<ILlmGpuRelease>() is not { } release || (await settings.GetAsync(cancellationToken)).Backend != AgentBackendDto.Ollama)
        {
            return;
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        try
        {
            await release.ReleaseAsync(limit.Token).WaitAsync(Limit, clock, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            await limit.CancelAsync();
        }
    }
}
