using System.Text.Json.Serialization;

namespace StemStudio.Contracts.InstrumentViews;

/// <summary>Instrument family ("Practise as") (backend-api 1.15.0, feature 016, data-model 5.3).</summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstrumentFamilyDto>))]
public enum InstrumentFamilyDto
{
    /// <summary>Lead and backing vocals.</summary>
    [JsonStringEnumMemberName("vocals")]
    Vocals,

    /// <summary>Drum kit and percussion.</summary>
    [JsonStringEnumMemberName("drums")]
    Drums,

    /// <summary>Bass.</summary>
    [JsonStringEnumMemberName("bass")]
    Bass,

    /// <summary>Acoustic and electric guitar.</summary>
    [JsonStringEnumMemberName("guitar")]
    Guitar,

    /// <summary>Piano, keys and synth.</summary>
    [JsonStringEnumMemberName("keys")]
    Keys,

    /// <summary>Strings, brass and woodwinds.</summary>
    [JsonStringEnumMemberName("orchestral")]
    Orchestral,
}
