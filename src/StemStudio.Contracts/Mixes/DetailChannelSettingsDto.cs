namespace StemStudio.Contracts.Mixes;

/// <summary>Mixer settings of one detail stem channel (feature 002, FR-015).</summary>
/// <param name="Parent">Parent stem, see <see cref="DetailNames.Parents"/>.</param>
/// <param name="Stem">Detail stem name; must exist on the track under <paramref name="Parent"/>.</param>
/// <param name="Volume">Fader 0..1 (default 0.8).</param>
/// <param name="Pan">Pan -1 (left) .. 1 (right), default 0.</param>
/// <param name="Mute">Muted.</param>
/// <param name="Solo">Soloed.</param>
public sealed record DetailChannelSettingsDto(string Parent, string Stem, double Volume, double Pan, bool Mute, bool Solo);
