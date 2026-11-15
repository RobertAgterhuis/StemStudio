using System.Globalization;
using StemStudio.Contracts.Tracks;

namespace StemStudio.Web.Services;

/// <summary>Display texts for the analysis results of a track.</summary>
public static class TrackFacts
{
    /// <summary>Name of the 4-stem fallback model.</summary>
    public const string FallbackModel = "htdemucs";

    /// <summary>Detected tempo, e.g. "113.5 BPM", or "not detected".</summary>
    /// <param name="track">The track.</param>
    /// <returns>The tempo text.</returns>
    public static string Bpm(TrackDto track) => track.BeatGrid is { Bpm: > 0 } grid
        ? string.Create(CultureInfo.InvariantCulture, $"{grid.Bpm:0.#} BPM")
        : "not detected";

    /// <summary>Model name with its stem count, e.g. "htdemucs_6s (6 stems)".</summary>
    /// <param name="track">The track.</param>
    /// <returns>The model text.</returns>
    public static string Model(TrackDto track) => $"{track.Model} ({track.Stems.Count} stems)";

    /// <summary>True when the 4-stem fallback model produced the stems.</summary>
    /// <param name="track">The track.</param>
    /// <returns>Whether the fallback was used.</returns>
    public static bool IsFallback(TrackDto track) => track.Model == FallbackModel;
}
