namespace StemStudio.Domain.Tracks;

/// <summary>
/// Immutable record of where a standard or detail job ran (feature 003, data-model.md 2.2 and 3.1). The constructor
/// enforces the value rules, so worker data that breaks them never reaches a track.
/// </summary>
public sealed record JobDevice
{
    /// <summary>Maximum GPU name length.</summary>
    public const int MaxNameLength = 200;

    /// <summary>Maximum fallback reason length.</summary>
    public const int MaxFallbackReasonLength = 500;

    /// <summary>Creates a validated device record.</summary>
    /// <param name="kind">GPU once any model step ran on the GPU, else CPU.</param>
    /// <param name="name">GPU name (&lt;= 200 characters); null for the CPU.</param>
    /// <param name="cpuFallback">Whether a model step ran on the CPU on a GPU worker.</param>
    /// <param name="fallbackReason">Reason of the latest fallback (&lt;= 500 characters); set iff <paramref name="cpuFallback"/>.</param>
    /// <exception cref="DomainValidationException">A rule is violated.</exception>
    public JobDevice(ProcessingDeviceKind kind, string? name, bool cpuFallback, string? fallbackReason)
    {
        Validate(kind, name, cpuFallback, fallbackReason);
        (Kind, Name, CpuFallback, FallbackReason) = (kind, name, cpuFallback, fallbackReason);
    }

    /// <summary>GPU or CPU.</summary>
    public ProcessingDeviceKind Kind { get; }

    /// <summary>GPU name; null for the CPU.</summary>
    public string? Name { get; }

    /// <summary>Whether a model step fell back to the CPU.</summary>
    public bool CpuFallback { get; }

    /// <summary>Readable reason of the latest fallback; null without one.</summary>
    public string? FallbackReason { get; }

    private static void Validate(ProcessingDeviceKind kind, string? name, bool cpuFallback, string? fallbackReason)
    {
        var errors = new ValidationErrors();
        errors.Check(Enum.IsDefined(kind), "kind", "Must be cpu or gpu.");
        errors.Check(kind == ProcessingDeviceKind.Gpu || name is null, "name", "Must be null for the CPU.");
        errors.Check((name?.Length ?? 0) <= MaxNameLength, "name", $"Must be at most {MaxNameLength} characters.");
        errors.Check((fallbackReason is not null) == cpuFallback, "fallbackReason", "Must be set exactly when the job fell back to the CPU.");
        errors.Check((fallbackReason?.Length ?? 0) <= MaxFallbackReasonLength, "fallbackReason", $"Must be at most {MaxFallbackReasonLength} characters.");
        errors.ThrowIfAny();
    }
}
