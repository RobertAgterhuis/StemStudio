namespace StemStudio.Web.Agent;

/// <summary>Fader gain (0..1, linear) to decibels and back for the agent's <c>gainDb</c> arguments (-60 dB = silent).</summary>
public static class Gain
{
    /// <summary>The lowest reported level; at or below it the fader is at 0.</summary>
    public const double FloorDb = -60;

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
}
