namespace StemStudio.Contracts.Scores;

/// <summary>The key of a score.</summary>
/// <param name="Tonic">Tonic in music21 spelling (<c>A-</c> = A-flat).</param>
/// <param name="Mode">Major or minor.</param>
/// <param name="Confidence">0..1 (1 for overrides).</param>
/// <param name="Source">Estimated or override.</param>
public sealed record KeyInfoDto(string Tonic, KeyModeDto Mode, double Confidence, KeySourceDto Source);
