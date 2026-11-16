namespace StemStudio.Agent.Ports;

/// <summary>GPU state seen by the API (FR-021). Implemented by the backend from its own job states (GpuActivity, T053).</summary>
public interface IGpuActivity
{
    /// <summary>True while any GPU-heavy job is queued or running on a GPU worker (standard, detail, 006-010 jobs).</summary>
    /// <returns>True when the agent must use the CPU fallback model.</returns>
    bool IsHeavyJobActive();
}
