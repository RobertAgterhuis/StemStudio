using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Drum piece of the drum trainer (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DrumPieceDto>))]
public enum DrumPieceDto
{
    /// <summary>Kick drum.</summary>
    [JsonStringEnumMemberName("kick")]
    Kick,

    /// <summary>Snare drum.</summary>
    [JsonStringEnumMemberName("snare")]
    Snare,

    /// <summary>Toms.</summary>
    [JsonStringEnumMemberName("toms")]
    Toms,

    /// <summary>Hi-hat.</summary>
    [JsonStringEnumMemberName("hihat")]
    Hihat,

    /// <summary>Cymbals.</summary>
    [JsonStringEnumMemberName("cymbals")]
    Cymbals,
}
