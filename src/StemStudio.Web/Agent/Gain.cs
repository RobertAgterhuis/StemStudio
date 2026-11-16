namespace StemStudio.Web.Agent;

/// <summary>Fader gain (0..1, linear) to decibels and back for the agent's <c>gainDb</c> arguments (-60 dB = silent).</summary>
public static class Gain
{
    /// <summary>The lowest reported level; at or below it the fader is at 0.</summary>
    public const double FloorDb = -60;

    /// <summary>The highest level: the fader maximum (gain 1). Higher requests are capped here.</summary>
    public const double MaxDb = 0;

    /// <summary>The level in dB, unrounded (the floor for silence).</summary>
    /// <param name="volume">The linear gain.</param>
    /// <returns>The level.</returns>
    public static double RawDb(double volume) => volume > 0 ? Math.Max(FloorDb, 20 * Math.Log10(volume)) : FloorDb;

    /// <summary>The level in dB rounded to 0.1 dB for results.</summary>
    /// <param name="volume">The linear gain.</param>
    /// <returns>The level.</returns>
    public static double ToDb(double volume) => Math.Round(RawDb(volume), 1);

    /// <summary>The linear gain of a level (0 at or below the floor).</summary>
    /// <param name="db">The level.</param>
    /// <returns>The gain.</returns>
    public static double ToVolume(double db) => db <= FloorDb ? 0 : Math.Pow(10, db / 20);

    /// <summary>True when a requested level lies above the fader maximum, so the fader stopped at <see cref="MaxDb"/>.</summary>
    /// <param name="requestedDb">The requested level, or null when no level was asked.</param>
    /// <returns>Whether the request was capped.</returns>
    public static bool IsCapped(double? requestedDb) => requestedDb > MaxDb + 0.05;

    /// <summary>A requested level rounded to 0.1 dB for results, or null.</summary>
    /// <param name="requestedDb">The requested level.</param>
    /// <returns>The rounded level.</returns>
    public static double? Requested(double? requestedDb) => requestedDb is { } db ? Math.Round(db, 1) : null;
}
