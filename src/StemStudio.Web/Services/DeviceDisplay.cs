using StemStudio.Contracts.Health;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>What a <c>DeviceBadge</c> shows (ui-test-ids 1.2.0, FR-006).</summary>
/// <param name="Kind">Value of <c>data-device</c>: <c>gpu</c>, <c>cpu</c> or <c>unknown</c>.</param>
/// <param name="Text">Visible device text: <c>GPU (&lt;name&gt;)</c>, <c>CPU</c> or <c>Unknown</c>.</param>
/// <param name="Fallback">Value of <c>data-fallback</c>; null leaves the attribute out.</param>
/// <param name="Reason">Fallback or CPU reason; null hides the reason element.</param>
public sealed record DeviceDisplay(string Kind, string Text, bool? Fallback, string? Reason)
{
    /// <summary>Reason shown when a fallback is flagged without a reason.</summary>
    public const string UnknownFallbackReason = "A step ran on the CPU instead of the GPU.";

    /// <summary>Display of the device a standard or detail job ran on.</summary>
    /// <param name="device">The job device record.</param>
    /// <returns>The display.</returns>
    public static DeviceDisplay FromJob(JobDeviceDto device) => new(
        KindValue(device.Kind),
        device.DisplayText(),
        device.CpuFallback,
        device.CpuFallback ? device.FallbackReason ?? UnknownFallbackReason : null);

    /// <summary>Display of a worker device that is not known (worker unreachable or no answer).</summary>
    public static DeviceDisplay Unknown { get; } = new("unknown", "Unknown", null, null);

    /// <summary>Display of the worker's active processing device (<c>GET /api/device</c>).</summary>
    /// <param name="device">The answer, or null when the API could not be asked.</param>
    /// <returns>The display; <see cref="Unknown"/> without an answer.</returns>
    public static DeviceDisplay FromWorker(ProcessingDeviceDto? device) => device is { Reachable: true, Kind: { } kind }
        ? new(KindValue(kind), new JobDeviceDto(kind, device.Name, false, null).DisplayText(), null, device.Reason)
        : Unknown;

    private static string KindValue(ProcessingDeviceKindDto kind) =>
        kind == ProcessingDeviceKindDto.Gpu ? "gpu" : "cpu";
}
