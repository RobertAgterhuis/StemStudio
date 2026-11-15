using StemStudio.Contracts.Tracks;

namespace StemStudio.Contracts.Health;

/// <summary>Body of <c>GET /api/device</c>: the worker's active processing device (backend contract 1.3.0).</summary>
public sealed record ProcessingDeviceDto
{
    /// <summary>The answer when the worker cannot be reached (every device field null).</summary>
    public static ProcessingDeviceDto Unreachable { get; } = new() { Reachable = false };

    /// <summary>False when the worker could not be asked.</summary>
    public required bool Reachable { get; init; }

    /// <summary>GPU or CPU; null when unreachable.</summary>
    public ProcessingDeviceKindDto? Kind { get; init; }

    /// <summary>GPU name when <see cref="Kind"/> is GPU (&lt;= 200 characters).</summary>
    public string? Name { get; init; }

    /// <summary>Total GPU memory in MiB when <see cref="Kind"/> is GPU.</summary>
    public int? TotalMemoryMiB { get; init; }

    /// <summary>Why the GPU is not used although it was requested; null otherwise (&lt;= 500 characters).</summary>
    public string? Reason { get; init; }
}
