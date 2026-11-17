namespace StemStudio.Contracts.Mixes;

/// <summary>
/// Ranges of the practice settings (backend-api 1.11.0 <c>PracticeSettings</c>, <c>SpeedTrainerSettings</c>,
/// <c>CountInSettings</c>; data-model.md 2.1). Every value is an integer; the trainer also needs start &lt; target.
/// </summary>
public static class PracticeLimits
{
    /// <summary>Slowest speed in percent.</summary>
    public const int MinSpeedPercent = 50;

    /// <summary>Fastest speed in percent.</summary>
    public const int MaxSpeedPercent = 120;

    /// <summary>Lowest transpose in semitones.</summary>
    public const int MinTransposeSemitones = -12;

    /// <summary>Highest transpose in semitones.</summary>
    public const int MaxTransposeSemitones = 12;

    /// <summary>Highest trainer start speed in percent (the minimum is <see cref="MinSpeedPercent"/>).</summary>
    public const int MaxTrainerStartPercent = 119;

    /// <summary>Lowest trainer target speed in percent (the maximum is <see cref="MaxSpeedPercent"/>).</summary>
    public const int MinTrainerTargetPercent = 51;

    /// <summary>Smallest trainer step in percent.</summary>
    public const int MinTrainerStepPercent = 1;

    /// <summary>Largest trainer step in percent.</summary>
    public const int MaxTrainerStepPercent = 20;

    /// <summary>Fewest completed passes per trainer speed.</summary>
    public const int MinPassesPerStep = 1;

    /// <summary>Most completed passes per trainer speed.</summary>
    public const int MaxPassesPerStep = 8;

    /// <summary>Most count-in bars (0 = off).</summary>
    public const int MaxCountInBars = 2;

    /// <summary>Error message at <c>practice.trainer.targetPercent</c> when the target is not above the start.</summary>
    public const string TargetAboveStartMessage = "The target must be above the start speed.";
}
