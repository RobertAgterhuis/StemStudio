namespace StemStudio.Contracts.Tracks;

/// <summary>Detected tempo and beat positions of a track (spec FR-007).</summary>
/// <param name="Bpm">Global tempo estimate in beats per minute (0 when detection failed).</param>
/// <param name="Beats">Beat times in seconds from the start, ascending.</param>
/// <param name="Downbeats">Times of beat 1 of each bar in seconds, a subset of <paramref name="Beats"/>.</param>
/// <param name="BeatsPerBar">Assumed beats per bar (time signature numerator, default 4).</param>
/// <param name="Confidence">Detection confidence 0..1.</param>
/// <param name="Reliable">False when detection failed or is too uncertain; the metronome then starts disabled.</param>
public sealed record BeatGridDto(
    double Bpm,
    IReadOnlyList<double> Beats,
    IReadOnlyList<double> Downbeats,
    int BeatsPerBar,
    double Confidence,
    bool Reliable);
