namespace StemStudio.Contracts.Scores;

/// <summary>The quality note of a profile (what the transcription can and cannot do).</summary>
/// <param name="Profile">Profile.</param>
/// <param name="Text">English text (1..500 characters).</param>
public sealed record ProfileLimitDto(ScoreProfileDto Profile, string Text);
