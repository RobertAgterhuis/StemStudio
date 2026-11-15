using StemStudio.Contracts.Mixes;

namespace StemStudio.Contracts.Assets;

/// <summary>Mixer state used by an export: <see cref="MixSettingsDto"/> without name, metronome and loop.</summary>
/// <param name="MasterVolume">Master volume 0..1.</param>
/// <param name="Channels">At most one entry per stem; stems without an entry use the defaults.</param>
public sealed record ExportMixDto(double MasterVolume, IReadOnlyList<ChannelSettingsDto> Channels)
{
    /// <summary>Parents shown expanded (their detail channels replace them).</summary>
    public IReadOnlyList<string> Expanded { get; init; } = [];

    /// <summary>Detail channel settings; detail stems without an entry use the defaults.</summary>
    public IReadOnlyList<DetailChannelSettingsDto> DetailChannels { get; init; } = [];
}
