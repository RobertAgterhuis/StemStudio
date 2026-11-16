namespace StemStudio.Domain.Mixes;

/// <summary>
/// Practice tempo of a mix (feature 012, data-model.md 2.1): speed and key for the whole mix, the speed trainer and the
/// count-in. Never applied to exports. Mixes saved before 012 read <see cref="Default"/>.
/// </summary>
public sealed record PracticeSettings
{
    /// <summary>Normal speed in percent.</summary>
    public const int NormalSpeedPercent = 100;

    /// <summary>Message at <c>trainer.targetPercent</c> when the target is not above the start.</summary>
    public const string TargetAboveStartMessage = "The target must be above the start speed.";

    private static readonly Bounds SpeedBounds = new(50, 120);

    private static readonly Bounds TransposeBounds = new(-12, 12);

    /// <summary>The neutral defaults: 100 %, original key, trainer off, no count-in.</summary>
    public static PracticeSettings Default { get; } = new();

    /// <summary>Playback speed 50..120 % of the original tempo; the pitch is kept.</summary>
    public int SpeedPercent { get; init; } = NormalSpeedPercent;

    /// <summary>Pitch shift -12..12 semitones; the speed is kept.</summary>
    public int TransposeSemitones { get; init; }

    /// <summary>Speed trainer for the A-B loop.</summary>
    public SpeedTrainerSettings Trainer { get; init; } = SpeedTrainerSettings.Default;

    /// <summary>Count-in before playback and, optionally, before every loop pass.</summary>
    public CountInSettings CountIn { get; init; } = CountInSettings.Default;

    /// <summary>True when playback needs no time stretching: normal speed, original key, trainer off (the count-in does not matter).</summary>
    public bool IsNeutral => SpeedPercent == NormalSpeedPercent && TransposeSemitones == 0 && !Trainer.Enabled;

    /// <summary>Writes the violations into a collector (data-model.md 2.2).</summary>
    /// <param name="errors">Collector positioned at the practice settings.</param>
    internal void Validate(ValidationErrors errors)
    {
        errors.Range("speedPercent", SpeedPercent, SpeedBounds);
        errors.Range("transposeSemitones", TransposeSemitones, TransposeBounds);
        Trainer.Validate(errors.Nested("trainer"));
        CountIn.Validate(errors.Nested("countIn"));
    }
}
