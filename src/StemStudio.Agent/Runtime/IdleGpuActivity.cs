using StemStudio.Agent.Ports;

namespace StemStudio.Agent.Runtime;

/// <summary>Default <see cref="IGpuActivity"/> until the host registers its own: no heavy GPU job is known.</summary>
public sealed class IdleGpuActivity : IGpuActivity
{
    /// <inheritdoc />
    public bool IsHeavyJobActive() => false;
}
