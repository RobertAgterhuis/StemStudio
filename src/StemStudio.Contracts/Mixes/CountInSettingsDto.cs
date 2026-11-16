namespace StemStudio.Contracts.Mixes;

/// <summary>Count-in of a mix (backend-api 1.11.0, <c>CountInSettings</c>): metronome clicks before the music.</summary>
/// <param name="Bars">Counted bars 0..2; 0 = off (default 0).</param>
/// <param name="EveryPass">Also before every pass of an enabled loop (default false).</param>
public sealed record CountInSettingsDto(int Bars, bool EveryPass)
{
    /// <summary>No count-in.</summary>
    public static CountInSettingsDto Default { get; } = new(0, false);
}
