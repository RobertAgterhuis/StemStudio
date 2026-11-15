using System.Text.Json.Serialization;

namespace StemStudio.Contracts.Assets;

/// <summary>Export mode: raw stems or the mix.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ExportModeDto>))]
public enum ExportModeDto
{
    /// <summary>Gain 1, pan 0, master 1; mute and solo ignored.</summary>
    [JsonStringEnumMemberName("raw")]
    Raw,

    /// <summary>Fader, pan and master of the mix.</summary>
    [JsonStringEnumMemberName("mix")]
    Mix,
}
