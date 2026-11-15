using System.Globalization;
using StemStudio.Domain.Tracks;

namespace StemStudio.Domain.Lyrics;

/// <summary>When an alignment starts without a click (data-model 3.2): the worker setting allows it and the standard separation is completed.</summary>
public static class AutoAlignRule
{
    /// <summary>Whether the automatic alignment applies.</summary>
    /// <param name="auto">The worker's <c>STEMSTUDIO_ALIGN_AUTO</c>.</param>
    /// <param name="workerDevice">The worker's active device.</param>
    /// <param name="standardCompleted">Whether the track's standard separation is completed.</param>
    /// <returns>True when the alignment starts at once.</returns>
    public static bool Applies(AlignAuto auto, ProcessingDeviceKind workerDevice, bool standardCompleted) =>
        standardCompleted && (auto == AlignAuto.Always || (auto == AlignAuto.Gpu && workerDevice == ProcessingDeviceKind.Gpu));
}

/// <summary>Expected alignment wall time (the worker's <c>estimate_alignment_seconds</c>, research.md R-04 / Phase 0 P0-2).</summary>
public static class AlignmentEstimate
{
    private const double GpuSecondsPerAudioSecond = 0.03;
    private const double GpuOverheadSeconds = 20;
    private const double CpuSecondsPerAudioSecond = 0.8;
    private const double CpuOverheadSeconds = 15;
    private const double DownloadSeconds = 140;

    /// <summary>Seconds an alignment takes.</summary>
    /// <param name="durationSeconds">Audio duration.</param>
    /// <param name="device">The worker's device.</param>
    /// <param name="modelCached">Whether the speech model is already downloaded.</param>
    /// <returns>Whole seconds.</returns>
    public static int Seconds(double durationSeconds, ProcessingDeviceKind device, bool modelCached)
    {
        var seconds = device == ProcessingDeviceKind.Gpu
            ? (GpuSecondsPerAudioSecond * durationSeconds) + GpuOverheadSeconds
            : (CpuSecondsPerAudioSecond * durationSeconds) + CpuOverheadSeconds;
        return (int)Math.Round(seconds + (modelCached ? 0 : DownloadSeconds), MidpointRounding.AwayFromZero);
    }

    /// <summary>The readable estimate shown while the alignment is pending.</summary>
    /// <param name="seconds">Estimated seconds.</param>
    /// <param name="device">The worker's device.</param>
    /// <returns>E.g. "Aligning takes about 7 minutes on the CPU.".</returns>
    public static string Describe(int seconds, ProcessingDeviceKind device)
    {
        var minutes = Math.Max(1, (int)Math.Round(seconds / 60.0, MidpointRounding.AwayFromZero));
        var unit = minutes == 1 ? "minute" : "minutes";
        var where = device == ProcessingDeviceKind.Gpu ? "GPU" : "CPU";
        return string.Create(CultureInfo.InvariantCulture, $"Aligning takes about {minutes} {unit} on the {where}.");
    }
}

/// <summary>A progress report of a running alignment job.</summary>
/// <param name="Progress">Progress 0..100.</param>
/// <param name="Device">Where it runs, once known.</param>
/// <param name="EstimateSeconds">The worker's estimate, once known.</param>
public sealed record AlignmentReport(int Progress, JobDevice? Device, int? EstimateSeconds);
