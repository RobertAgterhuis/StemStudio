using System.Text.Json.Serialization;
using StemStudio.Contracts.Mixes;
using StemStudio.Contracts.Recording;

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

    /// <summary>The take strip of an unsaved mixer state (1.14.0, amended in round 014+015); absent on the wire when null.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TakeChannelSettingsDto? Take { get; init; }
}
