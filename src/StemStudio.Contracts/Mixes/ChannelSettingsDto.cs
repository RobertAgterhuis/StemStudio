namespace StemStudio.Contracts.Mixes;

/// <summary>Mixer settings of one stem channel.</summary>
/// <param name="Stem">Stem name; must be one of the stems of the track of the mix.</param>
/// <param name="Volume">Fader 0..1 (default 0.8).</param>
/// <param name="Pan">Pan -1 (left) .. 1 (right), default 0.</param>
/// <param name="Mute">Muted.</param>
/// <param name="Solo">Soloed.</param>
public sealed record ChannelSettingsDto(string Stem, double Volume, double Pan, bool Mute, bool Solo);
