namespace StemStudio.Contracts.Mixes;

/// <summary>Metronome settings of a mix (spec FR-014).</summary>
/// <param name="Enabled">Click on/off.</param>
/// <param name="Volume">Click volume 0..1.</param>
/// <param name="BpmOverride">Manual constant tempo 30..300, or null to follow the detected beat grid.</param>
/// <param name="OffsetMs">Grid offset in milliseconds, -2000..2000.</param>
/// <param name="BeatsPerBar">Beats per bar 1..12; beat 1 is accented.</param>
public sealed record MetronomeSettingsDto(bool Enabled, double Volume, double? BpmOverride, int OffsetMs, int BeatsPerBar);
