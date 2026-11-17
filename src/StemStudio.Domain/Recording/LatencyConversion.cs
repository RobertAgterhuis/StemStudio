namespace StemStudio.Domain.Recording;

/// <summary>Milliseconds, seconds and samples at 44.1 kHz (data-model rounding rule).</summary>
public static class LatencyConversion
{
    /// <summary><c>sign(ms) * floor(abs(ms) * 44.1 + 0.5)</c>.</summary>
    /// <param name="milliseconds">Milliseconds.</param>
    /// <returns>Samples.</returns>
    public static long ToSamples(double milliseconds) =>
        Math.Sign(milliseconds) * (long)Math.Floor((Math.Abs(milliseconds) * RecordingConstants.SampleRate / 1000.0) + 0.5);

    /// <summary>Samples as milliseconds, rounded to 0.01 ms (contract examples: 1,102 samples = 24.99 ms).</summary>
    /// <param name="samples">Samples.</param>
    /// <returns>Milliseconds.</returns>
    public static double ToMilliseconds(long samples) => Math.Round(samples * 1000.0 / RecordingConstants.SampleRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>Samples as seconds.</summary>
    /// <param name="samples">Samples.</param>
    /// <returns>Seconds.</returns>
    public static double ToSeconds(long samples) => samples / (double)RecordingConstants.SampleRate;

    /// <summary>The sample count of a track duration (rounded to the nearest sample).</summary>
    /// <param name="durationSeconds">Track duration.</param>
    /// <returns>Samples.</returns>
    public static long TrackSamples(double durationSeconds) =>
        (long)Math.Round(durationSeconds * RecordingConstants.SampleRate, MidpointRounding.AwayFromZero);
}
