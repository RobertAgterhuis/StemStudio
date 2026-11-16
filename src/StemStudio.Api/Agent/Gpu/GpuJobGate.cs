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
/// <para>B2 (SC-008): every gated submission first opens a <see cref="GpuSubmissions"/> hold, so <see cref="GpuActivity"/> sends
/// each agent turn to the CPU fallback from before the release until the caller has stored the job.</para>
/// </summary>
/// <param name="services">Resolves the optional <see cref="ILlmGpuRelease"/> and the agent settings (selected backend).</param>
/// <param name="devices">Cached worker device (<c>GET /api/device</c>).</param>
/// <param name="submissions">The submission holds read by <see cref="GpuActivity"/>.</param>
/// <param name="clock">Clock of the 2 s limit.</param>
public sealed class GpuJobGate(IServiceProvider services, ProcessingDeviceCache devices, GpuSubmissions submissions, TimeProvider clock)
{
    /// <summary>Longest wait for the release.</summary>
    public static readonly TimeSpan Limit = TimeSpan.FromSeconds(2);

    private static readonly IDisposable NoHold = new NoGpuHold();

    /// <summary>
    /// Holds the GPU and releases the local model's GPU memory before a worker job, unless the worker is known to run on the CPU.
    /// </summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The hold to dispose once the submission returned; the task never faults because of the release.</returns>
    public Task<IDisposable> BeforeGpuJobAsync(CancellationToken cancellationToken) =>
        devices.TryGet()?.Kind == ProcessingDeviceKindDto.Cpu ? Task.FromResult(NoHold) : HoldAndReleaseAsync(cancellationToken);

    /// <summary>
    /// Holds the GPU and releases the local model's GPU memory before a generator or singer job. These services run on the GPU
    /// only, so the worker device does not matter.
    /// </summary>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <returns>The hold to dispose once the submission returned; the task never faults because of the release.</returns>
    public Task<IDisposable> BeforeGeneratorJobAsync(CancellationToken cancellationToken) => HoldAndReleaseAsync(cancellationToken);

    private async Task<IDisposable> HoldAndReleaseAsync(CancellationToken cancellationToken)
    {
        var hold = submissions.Begin();
        try
        {
            await ReleaseAsync(cancellationToken);
            return hold;
        }
        catch (OperationCanceledException)
        {
            hold.Dispose();
            throw;
        }
    }

    private async Task<ILlmGpuRelease?> ReleaseOfOllamaAsync(CancellationToken cancellationToken) =>
        services.GetService<ILlmGpuRelease>() is { } release && await OllamaSelectedAsync(cancellationToken) ? release : null;

    private async Task<bool> OllamaSelectedAsync(CancellationToken cancellationToken) =>
        (await services.GetRequiredService<IAgentSettingsStore>().GetAsync(cancellationToken)).Backend == AgentBackendDto.Ollama;

    private async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        if (await ReleaseOfOllamaAsync(cancellationToken) is { } release)
        {
            await ReleaseWithinLimitAsync(release, cancellationToken);
        }
    }

    private async Task ReleaseWithinLimitAsync(ILlmGpuRelease release, CancellationToken cancellationToken)
    {
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

    private sealed class NoGpuHold : IDisposable
    {
        public void Dispose()
        {
            // Nothing is held for a job on a CPU worker.
        }
    }
}
