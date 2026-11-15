namespace StemStudio.Contracts.Scores;

/// <summary>Expected processing time of one profile (and piano quality) on the worker's device.</summary>
/// <param name="Profile">Profile.</param>
/// <param name="Quality">Set for the two piano estimates.</param>
/// <param name="Seconds">Whole seconds.</param>
public sealed record ScoreEstimateDto(ScoreProfileDto Profile, ScoreQualityDto? Quality, int Seconds);
