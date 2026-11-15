namespace StemStudio.Domain.Tracks;

/// <summary>Device a model step ran on (feature 003, data-model.md 2.2).</summary>
public enum ProcessingDeviceKind
{
    /// <summary>The CPU.</summary>
    Cpu,

    /// <summary>The NVIDIA GPU (CUDA).</summary>
    Gpu,
}
