namespace StemStudio.Contracts.Tracks;

/// <summary>
/// Where a standard or detail job ran (backend contract 1.3.0, data-model.md 2.2 of feature 003).
/// <see cref="Kind"/> is GPU when at least one model step ran on the GPU; <see cref="CpuFallback"/>
/// is true when at least one model step ran on the CPU on a GPU worker.
/// </summary>
/// <param name="Kind">GPU or CPU.</param>
/// <param name="Name">GPU name when <paramref name="Kind"/> is GPU, else null (&lt;= 200 characters).</param>
/// <param name="CpuFallback">Whether a model step fell back to the CPU.</param>
/// <param name="FallbackReason">Readable reason of the latest fallback; null without one (&lt;= 500 characters).</param>
public sealed record JobDeviceDto(ProcessingDeviceKindDto Kind, string? Name, bool CpuFallback, string? FallbackReason)
{
    /// <summary>Text shown for the device: <c>GPU (&lt;name&gt;)</c> or <c>CPU</c> (ui-test-ids 1.2.0).</summary>
    /// <returns>The display text.</returns>
    public string DisplayText() => Kind == ProcessingDeviceKindDto.Gpu ? $"GPU ({Name})" : "CPU";
}
