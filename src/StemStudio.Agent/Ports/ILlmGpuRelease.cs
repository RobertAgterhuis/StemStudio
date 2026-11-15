namespace StemStudio.Agent.Ports;

/// <summary>
/// Frees the GPU memory held by the local model before a heavy GPU job (contracts/DELTA.md section 5, ADR-012). Implemented
/// in the agent library (OllamaGpuRelease: <c>keep_alive: 0</c> for both models); called by the backend before it submits a
/// GPU-heavy job. Best effort: at most 2 s, never throws.
/// </summary>
public interface ILlmGpuRelease
{
    /// <summary>Unloads the local models; returns after at most 2 s.</summary>
    /// <param name="cancellationToken">Cancellation.</param>
    /// <returns>A task that never faults.</returns>
    Task ReleaseAsync(CancellationToken cancellationToken);
}
