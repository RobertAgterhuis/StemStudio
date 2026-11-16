using System.Globalization;
using StemStudio.Web.Audio;

namespace StemStudio.Web.Mixer;

/// <summary>Visible and spoken texts of the practice controls (ui-test-ids 1.11.0, FR-017); invariant culture, English.</summary>
public static class PracticeText
{
    /// <summary>Appended to the engine's reason while the time-stretch stage is unavailable.</summary>
    public const string UnavailableSuffix = "Playback works at the original speed and key.";

    /// <summary>Reason used when the engine gave none.</summary>
    public const string UnknownReason = "Time stretching is not available in this browser.";

    /// <summary>The quality hint below 70 % or beyond +-7 semitones (FR-008).</summary>
    public const string QualityHint =
        "Far from the original speed or key, time stretching smears attacks and colours voices. That is normal; closer settings sound cleaner.";

    /// <summary>A speed as shown, e.g. "75 %".</summary>
    /// <param name="percent">The speed.</param>
    /// <returns>The text.</returns>
    public static string Percent(int percent) => $"{Number(percent)} %";

    /// <summary>A speed as spoken, e.g. "75 percent".</summary>
    /// <param name="percent">The speed.</param>
    /// <returns>The text.</returns>
    public static string SpokenPercent(int percent) => $"{Number(percent)} percent";

    /// <summary>A transpose as shown: "+2", "0", "-3".</summary>
    /// <param name="semitones">The transpose.</param>
    /// <returns>The text.</returns>
    public static string Semitones(int semitones) => semitones > 0 ? $"+{Number(semitones)}" : Number(semitones);

    /// <summary>A transpose as spoken: "+2 semitones", "+1 semitone", "0 semitones", "-3 semitones".</summary>
    /// <param name="semitones">The transpose.</param>
    /// <returns>The text.</returns>
    public static string SpokenSemitones(int semitones) => $"{Semitones(semitones)} {(Math.Abs(semitones) == 1 ? "semitone" : "semitones")}";

    /// <summary>The effective tempo, e.g. "≈ 90 BPM".</summary>
    /// <param name="bpm">The rounded tempo.</param>
    /// <returns>The text.</returns>
    public static string EffectiveBpm(int bpm) => $"≈ {Number(bpm)} BPM";

    /// <summary>The <c>data-stretch</c> value: <c>bypass</c>, <c>loading</c>, <c>active</c> or <c>unavailable</c>.</summary>
    /// <param name="state">The stretch state.</param>
    /// <returns>The contract name.</returns>
    public static string StretchName(StretchState state) => state.ToString().ToLowerInvariant();

    /// <summary>The Expert diagnostics line, e.g. "Stretch: active, latency 106 ms, 0 dropouts".</summary>
    /// <param name="status">The engine snapshot.</param>
    /// <returns>The text.</returns>
    public static string StretchStatus(PracticeStatus status) =>
        $"Stretch: {StretchName(status.Stretch)}, latency {LatencyMs(status)} ms, {Number(status.Underruns)} {(status.Underruns == 1 ? "dropout" : "dropouts")}";

    /// <summary>The heard latency in whole milliseconds (<c>data-latency-ms</c>).</summary>
    /// <param name="status">The engine snapshot.</param>
    /// <returns>The rounded latency.</returns>
    public static string LatencyMs(PracticeStatus status) => Number((long)Math.Round(status.LatencyMs, MidpointRounding.AwayFromZero));

    /// <summary>The <c>practice-unavailable</c> text: the engine's reason and that playback still works.</summary>
    /// <param name="reason">The engine's reason, if any.</param>
    /// <returns>The text.</returns>
    public static string Unavailable(string? reason) =>
        $"{(string.IsNullOrWhiteSpace(reason) ? UnknownReason : reason.Trim())} {UnavailableSuffix}";

    /// <summary>An integer as invariant text.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    public static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
}
