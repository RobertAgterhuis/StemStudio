using StemStudio.Domain.Tracks;

namespace StemStudio.Api.Worker;

/// <summary>Device record of a worker job or detail job (worker contract 1.2.0 <c>JobDevice</c>); not yet validated.</summary>
/// <param name="Kind">GPU or CPU.</param>
/// <param name="Name">GPU name when the kind is GPU.</param>
/// <param name="CpuFallback">Whether a model step fell back to the CPU.</param>
/// <param name="FallbackReason">Reason of the latest fallback.</param>
public sealed record WorkerJobDevice(ProcessingDeviceKind Kind, string? Name, bool CpuFallback, string? FallbackReason);

/// <summary>Active processing device of the worker, probed once at start-up (worker contract 1.2.0 <c>DeviceInfo</c>).</summary>
public sealed record WorkerDeviceInfo
{
    /// <summary>GPU or CPU.</summary>
    public required ProcessingDeviceKind Kind { get; init; }

    /// <summary>GPU name; null on the CPU.</summary>
    public string? Name { get; init; }

    /// <summary>GPU compute capability, e.g. <c>12.0</c>.</summary>
    public string? ComputeCapability { get; init; }

    /// <summary>Total GPU memory in MiB.</summary>
    public int? TotalMemoryMiB { get; init; }

    /// <summary>Free GPU memory at start-up in MiB.</summary>
    public int? FreeMemoryMiB { get; init; }

    /// <summary>Why the CPU is used although a GPU was requested; null otherwise.</summary>
    public string? Reason { get; init; }
}

/// <summary>Body of the worker's <c>GET /health</c> (worker contract 1.2.0 <c>Health</c>).</summary>
public sealed record WorkerHealth
{
    /// <summary>Always <c>ok</c>.</summary>
    public required string Status { get; init; }

    /// <summary><c>demucs</c> or <c>fake</c>.</summary>
    public required string Separator { get; init; }

    /// <summary>Configured device preference: <c>cpu</c>, <c>cuda</c> or <c>auto</c>.</summary>
    public required string Device { get; init; }

    /// <summary>The device actually used.</summary>
    public required WorkerDeviceInfo DeviceInfo { get; init; }

    /// <summary>Worker contract version, e.g. <c>1.2.0</c>.</summary>
    public required string ContractVersion { get; init; }
}
