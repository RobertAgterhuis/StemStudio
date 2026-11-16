using StemStudio.Contracts.Mixes;

namespace StemStudio.Web.Mixer;

/// <summary>Practice tempo of <see cref="MixerState"/> (feature 012, FR-014).</summary>
public sealed partial class MixerState
{
    /// <summary>Current practice settings.</summary>
    public PracticeSettingsDto Practice { get; private set; } = PracticeSettingsDto.Default;

    /// <summary>Last non-zero count-in bar count.</summary>
    public int LastCountInBars => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="percent">Speed.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto SetSpeed(int percent) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="delta">Delta.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto NudgeSpeed(int delta) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="semitones">Semitones.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto SetTranspose(int semitones) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="delta">Delta.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto NudgeTranspose(int delta) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto ResetPractice() => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="trainer">Trainer.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto SetTrainer(SpeedTrainerSettingsDto trainer) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto ToggleTrainer() => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="bars">Bars.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto SetCountInBars(int bars) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="everyPass">Every pass.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto SetCountInEveryPass(bool everyPass) => throw new NotImplementedException();

    /// <summary>Stub.</summary>
    /// <param name="beginner">Beginner.</param>
    /// <returns>Settings.</returns>
    public PracticeSettingsDto ToggleCountIn(bool beginner) => throw new NotImplementedException();
}
