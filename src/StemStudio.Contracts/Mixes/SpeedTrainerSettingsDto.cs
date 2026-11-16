namespace StemStudio.Contracts.Mixes;

/// <summary>
/// Speed trainer of a mix (backend-api 1.11.0, <c>SpeedTrainerSettings</c>): pass 1 of the enabled loop plays at
/// <paramref name="StartPercent"/>; after every <paramref name="PassesPerStep"/> completed passes the speed rises by
/// <paramref name="StepPercent"/>, never above <paramref name="TargetPercent"/>. Requires start &lt; target.
/// </summary>
/// <param name="Enabled">Trainer on; effective only while the mix has an enabled loop of at least 0.5 s (default false).</param>
/// <param name="StartPercent">Speed of pass 1, 50..119 % (default 70).</param>
/// <param name="StepPercent">Increase per step, 1..20 % (default 5).</param>
/// <param name="TargetPercent">Final speed, 51..120 % and above the start (default 100).</param>
/// <param name="PassesPerStep">Completed passes per speed, 1..8 (default 1).</param>
public sealed record SpeedTrainerSettingsDto(bool Enabled, int StartPercent, int StepPercent, int TargetPercent, int PassesPerStep)
{
    /// <summary>Trainer off, 70 % -> 100 % in steps of 5 % after every pass.</summary>
    public static SpeedTrainerSettingsDto Default { get; } = new(false, 70, 5, 100, 1);
}
