namespace StemStudio.Domain.Mixes;

/// <summary>
/// Speed trainer of a mix (feature 012): pass 1 of the enabled loop plays at <see cref="StartPercent"/>; after every
/// <see cref="PassesPerStep"/> completed passes the speed rises by <see cref="StepPercent"/>, never above
/// <see cref="TargetPercent"/>. Saving it without a loop is allowed (it is then inactive).
/// </summary>
public sealed record SpeedTrainerSettings
{
    // Literals, not derived from PracticeSettings: a cross-type static initialiser could see a half-built Default.
    private static readonly Bounds StartBounds = new(50, 119);
    private static readonly Bounds TargetBounds = new(51, 120);
    private static readonly Bounds StepBounds = new(1, 20);
    private static readonly Bounds PassesBounds = new(1, 8);

    /// <summary>Trainer off, 70 % to 100 % in steps of 5 % after every pass.</summary>
    public static SpeedTrainerSettings Default { get; } = new();

    /// <summary>Trainer on; effective only while the mix has an enabled loop of at least 0.5 s.</summary>
    public bool Enabled { get; init; }

    /// <summary>Speed of pass 1, 50..119 %.</summary>
    public int StartPercent { get; init; } = 70;

    /// <summary>Increase per step, 1..20 %.</summary>
    public int StepPercent { get; init; } = 5;

    /// <summary>Final speed, 51..120 % and above <see cref="StartPercent"/>.</summary>
    public int TargetPercent { get; init; } = PracticeSettings.NormalSpeedPercent;

    /// <summary>Completed passes per speed, 1..8.</summary>
    public int PassesPerStep { get; init; } = 1;

    /// <summary>
    /// Writes the violations into a collector; start &lt; target holds also while the trainer is off and is reported
    /// only when the target itself is within its range (one error per field).
    /// </summary>
    /// <param name="errors">Collector positioned at the trainer.</param>
    internal void Validate(ValidationErrors errors)
    {
        errors.Range("startPercent", StartPercent, StartBounds);
        errors.Range("stepPercent", StepPercent, StepBounds);
        var targetInRange = TargetBounds.Contains(TargetPercent);
        errors.Check(targetInRange, "targetPercent", TargetBounds.Message);
        errors.Check(!targetInRange || StartPercent < TargetPercent, "targetPercent", PracticeSettings.TargetAboveStartMessage);
        errors.Range("passesPerStep", PassesPerStep, PassesBounds);
    }
}
