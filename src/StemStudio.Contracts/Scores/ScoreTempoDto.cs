namespace StemStudio.Contracts.Scores;

/// <summary>A manual tempo; overrides the beat grid when given.</summary>
/// <param name="Bpm">30..300.</param>
/// <param name="BeatsPerBar">1..12 (time signature n/4).</param>
/// <param name="FirstDownbeatSeconds">Time of the first downbeat relative to the source (range) start, 0..900.</param>
public sealed record ScoreTempoDto(double Bpm, int BeatsPerBar, double FirstDownbeatSeconds);
