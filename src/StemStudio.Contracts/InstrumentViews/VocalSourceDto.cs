using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Stem the reference melody was taken from (backend-api 1.15.0, feature 016).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<VocalSourceDto>))]
public enum VocalSourceDto
{
    /// <summary>The kept lead vocal detail stem.</summary>
    [JsonStringEnumMemberName("vocals.lead")]
    VocalsLead,

    /// <summary>The standard vocals stem.</summary>
    [JsonStringEnumMemberName("vocals")]
    Vocals,
}
