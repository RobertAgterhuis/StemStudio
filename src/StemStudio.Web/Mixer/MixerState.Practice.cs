using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>
/// Practice tempo of <see cref="MixerState"/> (feature 012, FR-014): speed, transpose, speed trainer and count-in. Every
/// mutator clamps to <see cref="PracticeLimits"/>, marks the mixer dirty and returns the settings for the engine.
/// </summary>
public sealed partial class MixerState
{
    private int _lastCountInBars = 1;

    /// <summary>Current practice settings (saved with the mix; <see cref="PracticeSettingsDto.Default"/> for a new mixer).</summary>
    public PracticeSettingsDto Practice { get; private set; } = PracticeSettingsDto.Default;

    /// <summary>The last non-zero count-in bar count (1 until one is chosen); the Advanced count-in switch restores it.</summary>
    public int LastCountInBars => _lastCountInBars;

    /// <summary>Sets the speed (clamped 50..120 %).</summary>
    /// <param name="percent">The speed in percent.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto SetSpeed(int percent) =>
        ChangePractice(Practice with { SpeedPercent = Math.Clamp(percent, PracticeLimits.MinSpeedPercent, PracticeLimits.MaxSpeedPercent) });

    /// <summary>Changes the speed by a number of percent (clamped).</summary>
    /// <param name="delta">The change, e.g. -5.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto NudgeSpeed(int delta) => SetSpeed(Practice.SpeedPercent + delta);

    /// <summary>Sets the transpose (clamped -12..12 semitones).</summary>
    /// <param name="semitones">The transpose.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto SetTranspose(int semitones) =>
        ChangePractice(Practice with
        {
            TransposeSemitones = Math.Clamp(semitones, PracticeLimits.MinTransposeSemitones, PracticeLimits.MaxTransposeSemitones),
        });

    /// <summary>Changes the transpose by a number of semitones (clamped).</summary>
    /// <param name="delta">The change, e.g. 1.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto NudgeTranspose(int delta) => SetTranspose(Practice.TransposeSemitones + delta);

    /// <summary>"Original speed and key": 100 % and 0 semitones; trainer and count-in settings stay.</summary>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto ResetPractice() => ChangePractice(Practice with { SpeedPercent = 100, TransposeSemitones = 0 });

    /// <summary>Replaces the trainer settings (validated by the caller: start below target).</summary>
    /// <param name="trainer">The trainer settings.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto SetTrainer(SpeedTrainerSettingsDto trainer) => ChangePractice(Practice with { Trainer = trainer });

    /// <summary>Turns the speed trainer on or off.</summary>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto ToggleTrainer() => SetTrainer(Practice.Trainer with { Enabled = !Practice.Trainer.Enabled });

    /// <summary>Sets the count-in bars (clamped 0..2; 0 = off) and remembers a non-zero value.</summary>
    /// <param name="bars">The bar count.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto SetCountInBars(int bars)
    {
        var clamped = Math.Clamp(bars, 0, PracticeLimits.MaxCountInBars);
        _lastCountInBars = clamped > 0 ? clamped : _lastCountInBars;
        return ChangePractice(Practice with { CountIn = Practice.CountIn with { Bars = clamped } });
    }

    /// <summary>Sets "before every loop pass".</summary>
    /// <param name="everyPass">The new value.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto SetCountInEveryPass(bool everyPass) =>
        ChangePractice(Practice with { CountIn = Practice.CountIn with { EveryPass = everyPass } });

    /// <summary>
    /// The count-in switch: off, or on with 1 bar (Beginner) or the last bar count (Advanced, Expert). The Beginner
    /// switch does not overwrite the remembered bar count.
    /// </summary>
    /// <param name="beginner">True at the Beginner level.</param>
    /// <returns>The settings for the engine.</returns>
    public PracticeSettingsDto ToggleCountIn(bool beginner)
    {
        var bars = Practice.CountIn.Bars > 0 ? 0 : beginner ? 1 : _lastCountInBars;
        return ChangePractice(Practice with { CountIn = Practice.CountIn with { Bars = bars } });
    }

    private void ApplyPractice(PracticeSettingsDto? practice)
    {
        Practice = practice ?? PracticeSettingsDto.Default;
        _lastCountInBars = Practice.CountIn.Bars > 0 ? Practice.CountIn.Bars : _lastCountInBars;
    }

    private PracticeSettingsDto ChangePractice(PracticeSettingsDto practice)
    {
        Practice = practice;
        IsDirty = true;
        return practice;
    }
}
