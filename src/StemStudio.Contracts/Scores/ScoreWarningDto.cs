namespace StemStudio.Contracts.Scores;

/// <summary>A warning shown with a score.</summary>
/// <param name="Code">Warning code.</param>
/// <param name="Text">English text (1..300 characters).</param>
public sealed record ScoreWarningDto(ScoreWarningCodeDto Code, string Text);
