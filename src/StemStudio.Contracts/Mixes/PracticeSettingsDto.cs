namespace StemStudio.Contracts.Mixes;

/// <summary>
/// Practice tempo of a mix (backend-api 1.11.0, feature 012, <c>PracticeSettings</c>): speed and key for the whole
/// mix, the speed trainer and the count-in. Never applied to exports. Absent in a request = <see cref="Default"/>.
/// </summary>
/// <param name="SpeedPercent">Playback speed 50..120 % of the original tempo; the pitch is kept (default 100).</param>
/// <param name="TransposeSemitones">Pitch shift -12..12 semitones; the speed is kept (default 0).</param>
/// <param name="Trainer">Speed trainer for the A-B loop.</param>
/// <param name="CountIn">Count-in clicks before playback and, optionally, before every loop pass.</param>
public sealed record PracticeSettingsDto(int SpeedPercent, int TransposeSemitones, SpeedTrainerSettingsDto Trainer, CountInSettingsDto CountIn)
{
    /// <summary>The neutral defaults (100 %, 0 semitones, trainer off, no count-in); also what pre-1.11.0 mixes return.</summary>
    public static PracticeSettingsDto Default { get; } = new(100, 0, SpeedTrainerSettingsDto.Default, CountInSettingsDto.Default);
}
